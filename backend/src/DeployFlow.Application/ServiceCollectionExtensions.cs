using System.Reflection;
using DeployFlow.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeployFlow.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationLayer(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));
        });

        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddAutoMapper(Assembly.GetExecutingAssembly());

        return services;
    }
}

// ─── Audit Pipeline Behavior ─────────────────────────────────────────────────

public class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IAuditService _audit;
    private readonly ICurrentUser _currentUser;

    public AuditBehavior(IAuditService audit, ICurrentUser currentUser)
    {
        _audit = audit;
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;

        TResponse response;
        try
        {
            response = await next();
        }
        catch (UnauthorizedAccessException)
        {
            if (_currentUser.IsAuthenticated)
                FireAndForgetAudit(requestName, ":denied", ct);
            throw;
        }

        // Skip read-only queries — only audit mutations
        if (!_currentUser.IsAuthenticated || IsReadOnly(requestName))
            return response;

        var result = IsSuccess(response);
        FireAndForgetAudit(requestName, result ? "" : ":failed", ct);

        return response;
    }

    private void FireAndForgetAudit(string requestName, string suffix, CancellationToken ignoredCt)
    {
        var (resourceType, verb) = ParseRequestName(requestName);
        var resourceId = TryGetResourceId();
        _ = _audit.LogAsync(
            _currentUser.UserId, _currentUser.Name,
            verb + suffix, resourceType, resourceId,
            requestName, _currentUser.TenantId,
            ct: CancellationToken.None);
    }

    private static (string resourceType, string verb) ParseRequestName(string name)
    {
        var verbs = new[] { "Create", "Update", "Delete", "Cancel", "Retry", "Trigger", "Enable", "Disable", "Invite", "Remove", "Get", "List" };
        foreach (var v in verbs)
        {
            if (name.StartsWith(v, StringComparison.OrdinalIgnoreCase))
            {
                var resource = name[v.Length..].Replace("Command", "").Replace("Query", "");
                return (resource, v.ToLowerInvariant());
            }
        }
        return (name.Replace("Command", "").Replace("Query", ""), "execute");
    }

    private static bool IsReadOnly(string name) =>
        name.EndsWith("Query", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Get", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("List", StringComparison.OrdinalIgnoreCase);

    private Guid TryGetResourceId()
    {
        // Not needed at behavior level — resource ID captured per-handler if needed
        return Guid.Empty;
    }

    private static bool IsSuccess(TResponse response)
    {
        if (response is Result r) return r.IsSuccess;
        var prop = response?.GetType().GetProperty("IsSuccess");
        if (prop?.GetValue(response) is bool b) return b;
        return true;
    }
}

// ─── Validation Pipeline Behavior ─────────────────────────────────────────────

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!_validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = (await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, ct))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0) return await next();

        // If TResponse is Result-based, return failure
        var errors = string.Join("; ", failures.Select(f => f.ErrorMessage));

        if (typeof(TResponse) == typeof(Result))
            return (TResponse)(object)Result.Failure(errors, 400);

        if (typeof(TResponse).IsGenericType &&
            typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var innerType = typeof(TResponse).GetGenericArguments()[0];
            var method = typeof(Result<>)
                .MakeGenericType(innerType)
                .GetMethod(nameof(Result<object>.Failure), new[] { typeof(string), typeof(int) })!;
            return (TResponse)method.Invoke(null, new object[] { errors, 400 })!;
        }

        throw new FluentValidation.ValidationException(failures);
    }
}

// ─── Logging Pipeline Behavior ────────────────────────────────────────────────

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly Microsoft.Extensions.Logging.ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(Microsoft.Extensions.Logging.ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("Handling {RequestName}", requestName);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        if (sw.ElapsedMilliseconds > 500)
            _logger.LogWarning("Slow request {RequestName} took {Elapsed}ms", requestName, sw.ElapsedMilliseconds);
        else
            _logger.LogInformation("Handled {RequestName} in {Elapsed}ms", requestName, sw.ElapsedMilliseconds);

        return response;
    }
}
