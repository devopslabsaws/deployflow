using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;
using FluentValidation;
using System.Text.Json;
using System.Security.Cryptography;

namespace DeployFlow.Application.Features.ApiKeys;

// ─── DTOs ──────────────────────────────────────────────────────────────────────

public record ApiKeyDto(
    Guid Id,
    string Name,
    string KeyPrefix,
    string Permissions,
    DateTime CreatedAt,
    DateTime? LastUsed,
    bool IsEnabled
);

public record CreateApiKeyResultDto(ApiKeyDto Key, string FullKey);

// ─── Get API Keys ──────────────────────────────────────────────────────────────

public record GetApiKeysQuery : IRequest<Result<List<ApiKeyDto>>>;

public class GetApiKeysQueryHandler : IRequestHandler<GetApiKeysQuery, Result<List<ApiKeyDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetApiKeysQueryHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result<List<ApiKeyDto>>> Handle(GetApiKeysQuery request, CancellationToken ct)
    {
        var configs = await _uow.NotificationConfigs.GetByTenantAsync(_currentUser.TenantId, ct);
        var keys = configs
            .Where(c => c.Channel == "api_key")
            .Select(c => ToDto(c))
            .ToList();
        return Result<List<ApiKeyDto>>.Success(keys);
    }

    private static ApiKeyDto ToDto(NotificationConfig c)
    {
        string keyPrefix = "", permissions = "read", lastUsedStr = "";
        DateTime? lastUsed = null;
        try
        {
            var doc = JsonDocument.Parse(c.ConfigJson);
            doc.RootElement.TryGetProperty("keyPrefix", out var kp);
            doc.RootElement.TryGetProperty("permissions", out var perm);
            doc.RootElement.TryGetProperty("lastUsed", out var lu);
            keyPrefix = kp.GetString() ?? "";
            permissions = perm.GetString() ?? "read";
            lastUsedStr = lu.GetString() ?? "";
        }
        catch { }
        if (DateTime.TryParse(lastUsedStr, out var luDate)) lastUsed = luDate;
        return new ApiKeyDto(c.Id, c.Name, keyPrefix, permissions, c.CreatedAt, lastUsed, c.IsEnabled);
    }
}

// ─── Create API Key ────────────────────────────────────────────────────────────

public record CreateApiKeyCommand(string Name, string Permissions = "read") : IRequest<Result<CreateApiKeyResultDto>>;

public class CreateApiKeyCommandValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public class CreateApiKeyCommandHandler : IRequestHandler<CreateApiKeyCommand, Result<CreateApiKeyResultDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public CreateApiKeyCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result<CreateApiKeyResultDto>> Handle(CreateApiKeyCommand request, CancellationToken ct)
    {
        // Generate key: df_ + 32 random hex chars
        var randomBytes = RandomNumberGenerator.GetBytes(20);
        var token = "df_" + Convert.ToHexString(randomBytes).ToLower();
        var keyPrefix = token[..11]; // "df_" + first 8 hex chars

        var configJson = JsonSerializer.Serialize(new
        {
            keyPrefix,
            permissions = request.Permissions,
            lastUsed = (string?)null,
        });

        var cfg = new NotificationConfig
        {
            TenantId   = _currentUser.TenantId,
            Channel    = "api_key",
            Name       = request.Name,
            IsEnabled  = true,
            ConfigJson = configJson,
            EventsJson = "[]",
        };

        await _uow.NotificationConfigs.AddAsync(cfg, ct);
        await _uow.SaveChangesAsync(ct);

        var dto = new ApiKeyDto(cfg.Id, cfg.Name, keyPrefix, request.Permissions, cfg.CreatedAt, null, true);
        return Result<CreateApiKeyResultDto>.Success(new CreateApiKeyResultDto(dto, token));
    }
}

// ─── Delete API Key ────────────────────────────────────────────────────────────

public record DeleteApiKeyCommand(Guid Id) : IRequest<Result>;

public class DeleteApiKeyCommandHandler : IRequestHandler<DeleteApiKeyCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeleteApiKeyCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result> Handle(DeleteApiKeyCommand request, CancellationToken ct)
    {
        var configs = await _uow.NotificationConfigs.GetByTenantAsync(_currentUser.TenantId, ct);
        var cfg = configs.FirstOrDefault(c => c.Id == request.Id && c.Channel == "api_key");
        if (cfg is null)
            return Result.Failure("API key not found.", 404);

        await _uow.NotificationConfigs.DeleteAsync(cfg, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
