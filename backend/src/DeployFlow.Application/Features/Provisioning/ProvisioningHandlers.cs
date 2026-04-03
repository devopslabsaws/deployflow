using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using MediatR;

namespace DeployFlow.Application.Features.Provisioning;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record ProvisioningJobDto(
    Guid Id,
    string Name,
    string Provider,
    string Region,
    string Size,
    string Os,
    string Status,
    string? ProviderServerId,
    string? AssignedIpAddress,
    string? PlanOutput,
    string? ErrorMessage,
    Guid? CreatedServerId,
    DateTime? CompletedAt,
    DateTime CreatedAt
);

public record PlanServerRequest(
    string Name,
    string Provider,
    string Region,
    string Size,
    string Os,
    Guid? SshKeyId,
    Dictionary<string, string>? Tags
);

// ── Plan Query ────────────────────────────────────────────────────────────────

public record PlanServerProvisioningCommand(
    string Name,
    string Provider,
    string Region,
    string Size,
    string Os,
    Guid? SshKeyId,
    Dictionary<string, string>? Tags
) : IRequest<Result<ProvisioningJobDto>>;

public class PlanServerProvisioningCommandHandler
    : IRequestHandler<PlanServerProvisioningCommand, Result<ProvisioningJobDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    // Pricing estimates ($/hr) — rough guidelines for UI display only
    private static readonly Dictionary<string, Dictionary<string, decimal>> _pricing = new()
    {
        ["hetzner"] = new() { ["cx11"] = 0.006m, ["cx21"] = 0.010m, ["cx31"] = 0.020m, ["cx41"] = 0.040m, ["cx51"] = 0.070m },
        ["digitalocean"] = new() { ["s-1vcpu-1gb"] = 0.007m, ["s-2vcpu-2gb"] = 0.018m, ["s-4vcpu-8gb"] = 0.048m },
        ["aws"] = new() { ["t3.micro"] = 0.010m, ["t3.small"] = 0.020m, ["t3.medium"] = 0.042m, ["t3.large"] = 0.083m },
        ["gcp"] = new() { ["e2-micro"] = 0.008m, ["e2-small"] = 0.017m, ["e2-medium"] = 0.034m, ["n1-standard-1"] = 0.048m },
        ["azure"] = new() { ["Standard_B1ms"] = 0.021m, ["Standard_B2s"] = 0.042m, ["Standard_D2s_v3"] = 0.096m },
        ["vultr"] = new() { ["vc2-1c-1gb"] = 0.007m, ["vc2-2c-4gb"] = 0.030m, ["vc2-4c-8gb"] = 0.060m },
    };

    public PlanServerProvisioningCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<ProvisioningJobDto>> Handle(PlanServerProvisioningCommand request, CancellationToken ct)
    {
        // Generate a cloud-init script for the target provider
        var cloudInit = GenerateCloudInit(request.Os);

        var hourlyRate = _pricing.TryGetValue(request.Provider.ToLowerInvariant(), out var sizes)
            && sizes.TryGetValue(request.Size.ToLowerInvariant(), out var rate) ? rate : 0m;

        var planOutput = System.Text.Json.JsonSerializer.Serialize(new
        {
            provider = request.Provider,
            region = request.Region,
            size = request.Size,
            os = request.Os,
            estimated_hourly_cost = hourlyRate,
            estimated_monthly_cost = hourlyRate * 730,
            cloud_init_preview = cloudInit[..Math.Min(200, cloudInit.Length)],
            plan_time = DateTime.UtcNow
        });

        var job = new ProvisioningJob
        {
            TenantId = _currentUser.TenantId,
            Name = request.Name,
            Provider = request.Provider,
            Region = request.Region,
            Size = request.Size,
            Os = request.Os,
            SshKeyId = request.SshKeyId,
            Status = ProvisioningStatus.Pending,
            PlanOutput = planOutput,
            Tags = request.Tags ?? new()
        };

        await _uow.ProvisioningJobs.AddAsync(job, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<ProvisioningJobDto>.Success(Map(job));
    }

    private static string GenerateCloudInit(string os) => $"""
        #cloud-config
        package_update: true
        package_upgrade: true
        packages:
          - apt-transport-https
          - ca-certificates
          - curl
          - gnupg
          - lsb-release
          - fail2ban
          - ufw
        runcmd:
          - curl -fsSL https://get.docker.com | sh
          - systemctl enable docker
          - systemctl start docker
          - ufw allow 22/tcp
          - ufw allow 80/tcp
          - ufw allow 443/tcp
          - ufw --force enable
          - echo 'net.ipv4.ip_forward=1' >> /etc/sysctl.conf
          - sysctl -p
        """;

    internal static ProvisioningJobDto Map(ProvisioningJob j) => new(
        j.Id, j.Name, j.Provider, j.Region, j.Size, j.Os, j.Status.ToString().ToLower(),
        j.ProviderServerId, j.AssignedIpAddress, j.PlanOutput, j.ErrorMessage,
        j.CreatedServerId, j.CompletedAt, j.CreatedAt);
}

// ── Apply (actually provision via provider API) ───────────────────────────────

public record ApplyServerProvisioningCommand(Guid JobId) : IRequest<Result<ProvisioningJobDto>>;

public class ApplyServerProvisioningCommandHandler
    : IRequestHandler<ApplyServerProvisioningCommand, Result<ProvisioningJobDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public ApplyServerProvisioningCommandHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<ProvisioningJobDto>> Handle(ApplyServerProvisioningCommand request, CancellationToken ct)
    {
        var job = await _uow.ProvisioningJobs.GetByIdAsync(request.JobId, ct);
        if (job is null || job.TenantId != _currentUser.TenantId)
            return Result<ProvisioningJobDto>.Failure("Provisioning job not found.", "404");

        if (job.Status != ProvisioningStatus.Pending)
            return Result<ProvisioningJobDto>.Failure($"Job is in state '{job.Status}' — only Pending jobs can be applied.", "409");

        job.Status = ProvisioningStatus.Applying;
        await _uow.ProvisioningJobs.UpdateAsync(job, ct);
        await _uow.SaveChangesAsync(ct);

        // Background: actual provider API calls happen asynchronously via a dedicated
        // ProvisioningWorkerService (future sprint).  For now we return the job reference.
        return Result<ProvisioningJobDto>.Success(PlanServerProvisioningCommandHandler.Map(job));
    }
}

// ── Get Job ───────────────────────────────────────────────────────────────────

public record GetProvisioningJobQuery(Guid Id) : IRequest<Result<ProvisioningJobDto>>;

public class GetProvisioningJobQueryHandler : IRequestHandler<GetProvisioningJobQuery, Result<ProvisioningJobDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public GetProvisioningJobQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<ProvisioningJobDto>> Handle(GetProvisioningJobQuery request, CancellationToken ct)
    {
        var job = await _uow.ProvisioningJobs.GetByIdAsync(request.Id, ct);
        if (job is null || job.TenantId != _currentUser.TenantId)
            return Result<ProvisioningJobDto>.Failure("Provisioning job not found.", "404");

        return Result<ProvisioningJobDto>.Success(PlanServerProvisioningCommandHandler.Map(job));
    }
}

// ── List Jobs ─────────────────────────────────────────────────────────────────

public record ListProvisioningJobsQuery() : IRequest<Result<List<ProvisioningJobDto>>>;

public class ListProvisioningJobsQueryHandler : IRequestHandler<ListProvisioningJobsQuery, Result<List<ProvisioningJobDto>>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public ListProvisioningJobsQueryHandler(IUnitOfWork uow, ICurrentUser currentUser)
    {
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<List<ProvisioningJobDto>>> Handle(ListProvisioningJobsQuery request, CancellationToken ct)
    {
        var jobs = await _uow.ProvisioningJobs.GetByTenantAsync(_currentUser.TenantId, ct);
        return Result<List<ProvisioningJobDto>>.Success(
            jobs.OrderByDescending(j => j.CreatedAt)
                .Select(PlanServerProvisioningCommandHandler.Map)
                .ToList());
    }
}
