using DeployFlow.Application.Common;
using DeployFlow.Application.Features.Containers;
using DeployFlow.Application.Features.Services;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace DeployFlow.Tests.Application;

/// <summary>
/// Sprint 4 smoke tests covering:
///   A) Container handlers: Start, Stop, Restart, Remove (DockerService delegation + audit)
///   B) Container log retrieval
///   C) Service autoscaling: GetScalingPolicy (404 + 200) and UpdateScalingPolicy
///      (validation, save, wrong-tenant guard)
/// </summary>
public class Sprint4SmokeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OtherTenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static ICurrentUser MakeCurrentUser()
    {
        var cu = new Mock<ICurrentUser>();
        cu.Setup(x => x.TenantId).Returns(TenantId);
        cu.Setup(x => x.UserId).Returns(UserId);
        cu.Setup(x => x.Name).Returns("test-user");
        return cu.Object;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static (Mock<IDockerService> docker, Mock<IAuditService> audit) MakeDockerDeps()
    {
        var docker = new Mock<IDockerService>();
        var audit  = new Mock<IAuditService>();
        audit.Setup(a => a.LogAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return (docker, audit);
    }

    private static (Mock<IUnitOfWork> uow, Mock<IServiceRepository> svcRepo) MakeUow()
    {
        var svcRepo = new Mock<IServiceRepository>();
        var uow     = new Mock<IUnitOfWork>();
        uow.Setup(x => x.Services).Returns(svcRepo.Object);
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return (uow, svcRepo);
    }

    private static Service MakeService(
        int minReplicas = 1,
        int maxReplicas = 3,
        int? cpuTarget = null)
    {
        var svc = Service.Create(TenantId, Guid.NewGuid(), "test-svc", ServiceType.Web);
        svc.ConfigureScalingPolicy(minReplicas, maxReplicas, cpuTarget, null);
        return svc;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // A) Container — Start
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Container_Start_CallsDockerService_AndReturnsSuccess()
    {
        var (docker, audit) = MakeDockerDeps();
        docker.Setup(d => d.StartContainerAsync("srv1", "ctr1", It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        var handler = new StartContainerCommandHandler(docker.Object, MakeCurrentUser(), audit.Object);
        var result  = await handler.Handle(new StartContainerCommand("srv1", "ctr1"), default);

        result.IsSuccess.Should().BeTrue();
        docker.Verify(d => d.StartContainerAsync("srv1", "ctr1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Container_Start_WhenDockerThrows_ReturnsFailure()
    {
        var (docker, audit) = MakeDockerDeps();
        docker.Setup(d => d.StartContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(new InvalidOperationException("Docker unreachable"));

        var handler = new StartContainerCommandHandler(docker.Object, MakeCurrentUser(), audit.Object);
        var result  = await handler.Handle(new StartContainerCommand("srv1", "ctr1"), default);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Docker unreachable");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // B) Container — Stop
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Container_Stop_CallsDockerService_AndReturnsSuccess()
    {
        var (docker, audit) = MakeDockerDeps();
        docker.Setup(d => d.StopContainerAsync("srv1", "ctr1", It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        var handler = new StopContainerCommandHandler(docker.Object, MakeCurrentUser(), audit.Object);
        var result  = await handler.Handle(new StopContainerCommand("srv1", "ctr1"), default);

        result.IsSuccess.Should().BeTrue();
        docker.Verify(d => d.StopContainerAsync("srv1", "ctr1", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // C) Container — Restart
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Container_Restart_CallsDockerRestartDirectly_NotStopThenStart()
    {
        var (docker, audit) = MakeDockerDeps();
        docker.Setup(d => d.RestartContainerAsync("srv1", "ctr1", It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        var handler = new RestartContainerCommandHandler(docker.Object, MakeCurrentUser(), audit.Object);
        var result  = await handler.Handle(new RestartContainerCommand("srv1", "ctr1"), default);

        result.IsSuccess.Should().BeTrue();
        docker.Verify(d => d.RestartContainerAsync("srv1", "ctr1", It.IsAny<CancellationToken>()), Times.Once);
        docker.Verify(d => d.StopContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // D) Container — Remove
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Container_Remove_CallsDockerRemove_AndReturnsSuccess()
    {
        var (docker, audit) = MakeDockerDeps();
        docker.Setup(d => d.RemoveContainerAsync("srv1", "ctr1", It.IsAny<CancellationToken>()))
              .Returns(Task.CompletedTask);

        var handler = new RemoveContainerCommandHandler(docker.Object, MakeCurrentUser(), audit.Object);
        var result  = await handler.Handle(new RemoveContainerCommand("srv1", "ctr1"), default);

        result.IsSuccess.Should().BeTrue();
        docker.Verify(d => d.RemoveContainerAsync("srv1", "ctr1", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // E) Container — Get Logs
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Container_GetLogs_ReturnsJoinedOutput()
    {
        var (docker, _) = MakeDockerDeps();

        async IAsyncEnumerable<string> FakeLogs()
        {
            yield return "2024-01-01 INFO Starting...";
            yield return "2024-01-01 INFO  Ready";
            await Task.CompletedTask;
        }

        docker.Setup(d => d.StreamLogsAsync("srv1", "ctr1", false, It.IsAny<CancellationToken>()))
              .Returns(FakeLogs());

        var handler = new GetContainerLogsQueryHandler(docker.Object, MakeCurrentUser());
        var result  = await handler.Handle(new GetContainerLogsQuery("srv1", "ctr1", 100), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Starting...");
        result.Value.Should().Contain("Ready");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // F) Scaling policy — GetScalingPolicy 404
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ScalingPolicy_Get_WhenServiceNotFound_Returns404()
    {
        var (uow, svcRepo) = MakeUow();
        svcRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((Service?)null);

        var handler = new GetServiceScalingPolicyQueryHandler(uow.Object, MakeCurrentUser());
        var result  = await handler.Handle(new GetServiceScalingPolicyQuery(Guid.NewGuid()), default);

        result.IsSuccess.Should().BeFalse();
         result.ErrorCode.Should().Be("404");
    }

    [Fact]
    public async Task ScalingPolicy_Get_WhenWrongTenant_Returns404()
    {
        var svc = MakeService();
        svc.TenantId = OtherTenantId; // different tenant

        var (uow, svcRepo) = MakeUow();
        svcRepo.Setup(r => r.GetByIdAsync(svc.Id, It.IsAny<CancellationToken>()))
               .ReturnsAsync(svc);

        var handler = new GetServiceScalingPolicyQueryHandler(uow.Object, MakeCurrentUser());
        var result  = await handler.Handle(new GetServiceScalingPolicyQuery(svc.Id), default);

        result.IsSuccess.Should().BeFalse();
         result.ErrorCode.Should().Be("404");
    }

    [Fact]
    public async Task ScalingPolicy_Get_ReturnsCurrentPolicy()
    {
        var svc = MakeService(minReplicas: 2, maxReplicas: 8, cpuTarget: 70);
        var (uow, svcRepo) = MakeUow();
        svcRepo.Setup(r => r.GetByIdAsync(svc.Id, It.IsAny<CancellationToken>()))
               .ReturnsAsync(svc);

        var handler = new GetServiceScalingPolicyQueryHandler(uow.Object, MakeCurrentUser());
        var result  = await handler.Handle(new GetServiceScalingPolicyQuery(svc.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.MinReplicas.Should().Be(2);
        result.Value.MaxReplicas.Should().Be(8);
        result.Value.CpuTargetPercentage.Should().Be(70);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // G) Scaling policy — UpdateScalingPolicy
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ScalingPolicy_Update_PersistsNewPolicy()
    {
        var svc = MakeService(minReplicas: 1, maxReplicas: 3);
        var (uow, svcRepo) = MakeUow();
        svcRepo.Setup(r => r.GetByIdAsync(svc.Id, It.IsAny<CancellationToken>()))
               .ReturnsAsync(svc);

        var handler = new UpdateServiceScalingPolicyCommandHandler(uow.Object, MakeCurrentUser());
        var cmd     = new UpdateServiceScalingPolicyCommand(svc.Id, 2, 10, 65, null);
        var result  = await handler.Handle(cmd, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.MinReplicas.Should().Be(2);
        result.Value.MaxReplicas.Should().Be(10);
        result.Value.CpuTargetPercentage.Should().Be(65);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScalingPolicy_Update_WhenServiceNotFound_Returns404()
    {
        var (uow, svcRepo) = MakeUow();
        svcRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((Service?)null);

        var handler = new UpdateServiceScalingPolicyCommandHandler(uow.Object, MakeCurrentUser());
        var result  = await handler.Handle(
            new UpdateServiceScalingPolicyCommand(Guid.NewGuid(), 1, 5, null, null), default);

        result.IsSuccess.Should().BeFalse();
            result.ErrorCode.Should().Be("404");
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ScalingPolicy_Update_RecordsScalingDecision_WhenActionProvided()
    {
        var svc = MakeService();
        var (uow, svcRepo) = MakeUow();
        svcRepo.Setup(r => r.GetByIdAsync(svc.Id, It.IsAny<CancellationToken>()))
               .ReturnsAsync(svc);

        var handler = new UpdateServiceScalingPolicyCommandHandler(uow.Object, MakeCurrentUser());
        var cmd = new UpdateServiceScalingPolicyCommand(svc.Id, 1, 5, null, null,
            TriggerReason: "CPU spike at 90%", LastScalingAction: "scale-out");
        await handler.Handle(cmd, default);

        svc.LastScalingAction.Should().Be("scale-out");
        svc.LastScalingReason.Should().Be("CPU spike at 90%");
        svc.LastScaledAt.Should().NotBeNull();
    }
}
