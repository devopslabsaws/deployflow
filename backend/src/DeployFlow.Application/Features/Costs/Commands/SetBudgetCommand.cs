using DeployFlow.Application.Common;
using MediatR;

namespace DeployFlow.Application.Features.Costs.Commands;

// ── Command ───────────────────────────────────────────────────────────────────

public record SetBudgetCommand(decimal MonthlyBudget, decimal AlertAt = 80) : IRequest<Result>;

// ── Handler ───────────────────────────────────────────────────────────────────

public class SetBudgetCommandHandler : IRequestHandler<SetBudgetCommand, Result>
{
    private readonly ICacheService _cache;
    private readonly ICurrentUser  _currentUser;

    public SetBudgetCommandHandler(ICacheService cache, ICurrentUser currentUser)
    {
        _cache       = cache;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(SetBudgetCommand request, CancellationToken ct)
    {
        if (request.MonthlyBudget <= 0)
            return Result.Failure("Monthly budget must be a positive amount.");
        if (request.AlertAt is < 1 or > 100)
            return Result.Failure("AlertAt must be between 1 and 100 (percent).");

        var key   = $"budget:{_currentUser.TenantId}";
        var value = new { request.MonthlyBudget, request.AlertAt, SetAt = DateTime.UtcNow };
        await _cache.SetAsync(key, value, TimeSpan.FromDays(365), ct);

        return Result.Success();
    }
}
