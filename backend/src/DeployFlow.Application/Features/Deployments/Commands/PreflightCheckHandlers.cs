using DeployFlow.Application.Common;
using DeployFlow.Domain.Interfaces;
using MediatR;

namespace DeployFlow.Application.Features.Deployments.Commands;

// ── Preflight Check ───────────────────────────────────────────────────────────

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record PreflightCheckResult(string Name, string Status, string? Message)
{
    public bool Passed  => Status == "pass";
    public bool Warning => Status == "warn";
    public bool Failed  => Status == "fail";
}

public record PreflightReport(
    bool CanDeploy,
    IReadOnlyList<PreflightCheckResult> Checks,
    string GeneratedAt);

// ── Service interface (implemented in Infrastructure) ─────────────────────────

public interface IPreflightCheckService
{
    Task<PreflightReport> RunAsync(DeployFlow.Domain.Entities.Server server, string privateKey, int? port, CancellationToken ct);
}


public record PreflightCheckCommand(Guid ProjectId) : IRequest<Result<PreflightReport>>;

public class PreflightCheckCommandHandler : IRequestHandler<PreflightCheckCommand, Result<PreflightReport>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;
    private readonly IPreflightCheckService _preflight;

    public PreflightCheckCommandHandler(IUnitOfWork uow, ICurrentUser currentUser, IPreflightCheckService preflight)
    {
        _uow        = uow;
        _currentUser = currentUser;
        _preflight  = preflight;
    }

    public async Task<Result<PreflightReport>> Handle(PreflightCheckCommand request, CancellationToken ct)
    {
        var project = await _uow.Projects.GetByIdAsync(request.ProjectId, ct);
        if (project is null || project.TenantId != _currentUser.TenantId)
            return Result<PreflightReport>.Failure("Project not found.", 404);

        if (!project.ServerId.HasValue)
            return Result<PreflightReport>.Failure("Project has no server assigned — cannot run preflight checks.");

        var server = await _uow.Servers.GetByIdAsync(project.ServerId.Value, ct);
        if (server is null || server.TenantId != _currentUser.TenantId)
            return Result<PreflightReport>.Failure("Assigned server not found.", 404);

        if (!server.SshKeyId.HasValue)
            return Result<PreflightReport>.Failure("Server has no SSH key configured — cannot connect.");

        var sshKey = await _uow.SshKeys.GetByIdAsync(server.SshKeyId.Value, ct);
        if (sshKey is null)
            return Result<PreflightReport>.Failure("SSH key not found.", 404);

        // Run preflight — port from project config (null if not set)
        int? port = null;
        if (!string.IsNullOrWhiteSpace(project.StartCommand))
        {
            var portMatch = System.Text.RegularExpressions.Regex.Match(
                project.StartCommand, @"\b(\d{4,5})\b");
            if (portMatch.Success && int.TryParse(portMatch.Groups[1].Value, out var p))
                port = p;
        }

        var report = await _preflight.RunAsync(server, sshKey.PrivateKeyEncrypted, port, ct);
        return Result<PreflightReport>.Success(report);
    }
}
