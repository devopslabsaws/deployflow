using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using FluentValidation;
using MediatR;

namespace DeployFlow.Application.Features.Alerts;

public record AlertRuleDto(
    Guid Id,
    string Name,
    string Metric,
    string Operator,
    decimal Threshold,
    int WindowMinutes,
    string Severity,
    bool IsEnabled,
    int CooldownMinutes,
    DateTime? LastTriggeredAt,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record AlertRuleTestResultDto(
    Guid RuleId,
    bool Triggered,
    decimal SampleValue,
    string Message,
    Guid? AlertId
);

public record GetAlertRulesQuery : IRequest<Result<IReadOnlyList<AlertRuleDto>>>;

public class GetAlertRulesQueryHandler : IRequestHandler<GetAlertRulesQuery, Result<IReadOnlyList<AlertRuleDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetAlertRulesQueryHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result<IReadOnlyList<AlertRuleDto>>> Handle(GetAlertRulesQuery request, CancellationToken ct)
    {
        var rules = await _uow.AlertRules.GetByTenantAsync(_currentUser.TenantId, ct);
        var items = rules
            .OrderByDescending(r => r.UpdatedAt)
            .Select(ToDto)
            .ToList();
        return Result<IReadOnlyList<AlertRuleDto>>.Success(items);
    }

    private static AlertRuleDto ToDto(AlertRule rule) => new(
        rule.Id,
        rule.Name,
        rule.Metric,
        rule.Operator,
        rule.Threshold,
        rule.WindowMinutes,
        rule.Severity.ToString(),
        rule.IsEnabled,
        rule.CooldownMinutes,
        rule.LastTriggeredAt,
        rule.Description,
        rule.CreatedAt,
        rule.UpdatedAt);
}

public record CreateAlertRuleCommand(
    string Name,
    string Metric,
    string Operator,
    decimal Threshold,
    int WindowMinutes,
    string Severity,
    bool IsEnabled,
    int CooldownMinutes,
    string? Description
) : IRequest<Result<AlertRuleDto>>;

public class CreateAlertRuleCommandValidator : AbstractValidator<CreateAlertRuleCommand>
{
    public CreateAlertRuleCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Metric).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Operator).NotEmpty().Must(o => AllowedOperators.Contains(o));
        RuleFor(x => x.WindowMinutes).InclusiveBetween(1, 1440);
        RuleFor(x => x.CooldownMinutes).InclusiveBetween(0, 1440);
    }

    private static readonly HashSet<string> AllowedOperators = [">", ">=", "<", "<=", "=", "!="];
}

public class CreateAlertRuleCommandHandler : IRequestHandler<CreateAlertRuleCommand, Result<AlertRuleDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public CreateAlertRuleCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result<AlertRuleDto>> Handle(CreateAlertRuleCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<AlertSeverity>(request.Severity, true, out var severity))
            return Result<AlertRuleDto>.Failure("Invalid severity.", 400);

        var rule = new AlertRule
        {
            TenantId = _currentUser.TenantId,
            Name = request.Name.Trim(),
            Metric = request.Metric.Trim(),
            Operator = request.Operator.Trim(),
            Threshold = request.Threshold,
            WindowMinutes = request.WindowMinutes,
            Severity = severity,
            IsEnabled = request.IsEnabled,
            CooldownMinutes = request.CooldownMinutes,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()
        };

        await _uow.AlertRules.AddAsync(rule, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<AlertRuleDto>.Success(new AlertRuleDto(
            rule.Id,
            rule.Name,
            rule.Metric,
            rule.Operator,
            rule.Threshold,
            rule.WindowMinutes,
            rule.Severity.ToString(),
            rule.IsEnabled,
            rule.CooldownMinutes,
            rule.LastTriggeredAt,
            rule.Description,
            rule.CreatedAt,
            rule.UpdatedAt));
    }
}

public record UpdateAlertRuleCommand(
    Guid Id,
    string Name,
    string Metric,
    string Operator,
    decimal Threshold,
    int WindowMinutes,
    string Severity,
    bool IsEnabled,
    int CooldownMinutes,
    string? Description
) : IRequest<Result<AlertRuleDto>>;

public class UpdateAlertRuleCommandHandler : IRequestHandler<UpdateAlertRuleCommand, Result<AlertRuleDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public UpdateAlertRuleCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result<AlertRuleDto>> Handle(UpdateAlertRuleCommand request, CancellationToken ct)
    {
        var rule = await _uow.AlertRules.GetByIdAsync(request.Id, ct);
        if (rule is null || rule.TenantId != _currentUser.TenantId)
            return Result<AlertRuleDto>.Failure("Alert rule not found.", 404);

        if (!Enum.TryParse<AlertSeverity>(request.Severity, true, out var severity))
            return Result<AlertRuleDto>.Failure("Invalid severity.", 400);

        rule.Name = request.Name.Trim();
        rule.Metric = request.Metric.Trim();
        rule.Operator = request.Operator.Trim();
        rule.Threshold = request.Threshold;
        rule.WindowMinutes = request.WindowMinutes;
        rule.Severity = severity;
        rule.IsEnabled = request.IsEnabled;
        rule.CooldownMinutes = request.CooldownMinutes;
        rule.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        rule.UpdatedAt = DateTime.UtcNow;

        await _uow.AlertRules.UpdateAsync(rule, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<AlertRuleDto>.Success(new AlertRuleDto(
            rule.Id,
            rule.Name,
            rule.Metric,
            rule.Operator,
            rule.Threshold,
            rule.WindowMinutes,
            rule.Severity.ToString(),
            rule.IsEnabled,
            rule.CooldownMinutes,
            rule.LastTriggeredAt,
            rule.Description,
            rule.CreatedAt,
            rule.UpdatedAt));
    }
}

public record DeleteAlertRuleCommand(Guid Id) : IRequest<Result>;

public class DeleteAlertRuleCommandHandler : IRequestHandler<DeleteAlertRuleCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public DeleteAlertRuleCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result> Handle(DeleteAlertRuleCommand request, CancellationToken ct)
    {
        var rule = await _uow.AlertRules.GetByIdAsync(request.Id, ct);
        if (rule is null || rule.TenantId != _currentUser.TenantId)
            return Result.Failure("Alert rule not found.", 404);

        rule.SoftDelete(_currentUser.UserId);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public record TestAlertRuleCommand(Guid Id, decimal? SampleValue = null) : IRequest<Result<AlertRuleTestResultDto>>;

public class TestAlertRuleCommandHandler : IRequestHandler<TestAlertRuleCommand, Result<AlertRuleTestResultDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public TestAlertRuleCommandHandler(IUnitOfWork uow, ICurrentUser cu)
    { _uow = uow; _currentUser = cu; }

    public async Task<Result<AlertRuleTestResultDto>> Handle(TestAlertRuleCommand request, CancellationToken ct)
    {
        var rule = await _uow.AlertRules.GetByIdAsync(request.Id, ct);
        if (rule is null || rule.TenantId != _currentUser.TenantId)
            return Result<AlertRuleTestResultDto>.Failure("Alert rule not found.", 404);

        var sample = request.SampleValue ?? rule.Threshold + 1;
        var triggered = Evaluate(rule.Operator, sample, rule.Threshold);

        Guid? alertId = null;
        if (triggered)
        {
            var alert = new Alert
            {
                TenantId = _currentUser.TenantId,
                Name = $"Rule triggered: {rule.Name}",
                Description = rule.Description,
                Severity = rule.Severity,
                Status = AlertStatus.Active,
                Source = "alert-rule",
                ResourceId = rule.Id,
                ResourceType = "alert-rule",
                Condition = $"{rule.Metric} {rule.Operator} {rule.Threshold}",
                Threshold = rule.Threshold.ToString(System.Globalization.CultureInfo.InvariantCulture),
                TriggeredAt = DateTime.UtcNow
            };

            await _uow.Alerts.AddAsync(alert, ct);
            rule.LastTriggeredAt = DateTime.UtcNow;
            alertId = alert.Id;
        }

        await _uow.SaveChangesAsync(ct);

        var message = triggered
            ? "Rule condition matched and a test alert was generated."
            : "Rule condition did not match the sample value.";

        return Result<AlertRuleTestResultDto>.Success(new AlertRuleTestResultDto(rule.Id, triggered, sample, message, alertId));
    }

    private static bool Evaluate(string op, decimal sample, decimal threshold) => op switch
    {
        ">" => sample > threshold,
        ">=" => sample >= threshold,
        "<" => sample < threshold,
        "<=" => sample <= threshold,
        "=" => sample == threshold,
        "!=" => sample != threshold,
        _ => false
    };
}
