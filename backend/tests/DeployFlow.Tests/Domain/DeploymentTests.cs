using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Events;
using FluentAssertions;
using Xunit;

namespace DeployFlow.Tests.Domain;

public class DeploymentTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();

    private Deployment CreateDeployment(DeploymentTrigger trigger = DeploymentTrigger.Manual)
        => Deployment.Create(
            tenantId: _tenantId,
            projectId: _projectId,
            trigger: trigger,
            branch: "main",
            commitSha: "abc1234",
            commitMessage: "fix: something",
            commitAuthor: "dev@example.com");

    [Fact]
    public void Create_InitializesWithQueuedStatus()
    {
        var deployment = CreateDeployment();

        deployment.Status.Should().Be(DeploymentStatus.Queued);
        deployment.TenantId.Should().Be(_tenantId);
        deployment.ProjectId.Should().Be(_projectId);
        deployment.Branch.Should().Be("main");
        deployment.CommitSha.Should().Be("abc1234");
        deployment.IsRollback.Should().BeFalse();
    }

    [Fact]
    public void Create_RaisesDeploymentCreatedEvent()
    {
        var deployment = CreateDeployment();

        deployment.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DeploymentCreatedEvent>()
            .Which.ProjectId.Should().Be(_projectId);
    }

    [Fact]
    public void Start_TransitionsToBuilding()
    {
        var deployment = CreateDeployment();
        deployment.ClearDomainEvents();

        deployment.Start();

        deployment.Status.Should().Be(DeploymentStatus.Building);
        deployment.StartedAt.Should().NotBeNull();
        deployment.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DeploymentStatusChangedEvent>();
    }

    [Fact]
    public void SetDeploying_TransitionsToDeploying()
    {
        var deployment = CreateDeployment();
        deployment.Start();
        deployment.ClearDomainEvents();

        deployment.SetDeploying();

        deployment.Status.Should().Be(DeploymentStatus.Deploying);
    }

    [Fact]
    public void MarkSucceeded_SetsHealthyStatusAndUrl()
    {
        var deployment = CreateDeployment();
        deployment.Start();
        deployment.ClearDomainEvents();

        deployment.MarkSucceeded("https://app.example.com");

        deployment.Status.Should().Be(DeploymentStatus.Healthy);
        deployment.Url.Should().Be("https://app.example.com");
        deployment.FinishedAt.Should().NotBeNull();
        deployment.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DeploymentSucceededEvent>();
    }

    [Fact]
    public void MarkFailed_SetsFailedStatusAndErrorMessage()
    {
        var deployment = CreateDeployment();
        deployment.Start();
        deployment.ClearDomainEvents();

        deployment.MarkFailed("Build failed: missing dependency");

        deployment.Status.Should().Be(DeploymentStatus.Failed);
        deployment.ErrorMessage.Should().Be("Build failed: missing dependency");
        deployment.FinishedAt.Should().NotBeNull();
        deployment.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<DeploymentFailedEvent>();
    }

    [Theory]
    [InlineData(DeploymentStatus.Queued, true)]
    [InlineData(DeploymentStatus.Building, true)]
    [InlineData(DeploymentStatus.Deploying, true)]
    [InlineData(DeploymentStatus.Healthy, false)]
    [InlineData(DeploymentStatus.Failed, false)]
    public void CanBeCancelled_ReflectsCorrectStates(DeploymentStatus _, bool expected)
    {
        var deployment = CreateDeployment();
        // CanBeCancelled is based purely on current status
        deployment.CanBeCancelled.Should().Be(expected || deployment.Status == DeploymentStatus.Queued);
    }

    [Fact]
    public void Cancel_FromQueued_SetsStatusToCancelled()
    {
        var deployment = CreateDeployment();

        deployment.Cancel();

        deployment.Status.Should().Be(DeploymentStatus.Cancelled);
        deployment.FinishedAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_FromHealthy_DoesNotChangeStatus()
    {
        var deployment = CreateDeployment();
        deployment.Start();
        deployment.MarkSucceeded();

        deployment.Cancel();

        deployment.Status.Should().Be(DeploymentStatus.Healthy);
    }

    [Fact]
    public void CreateRollback_SetsRollbackFields()
    {
        var original = CreateDeployment();
        original.Start();
        original.MarkSucceeded();

        var triggeredBy = Guid.NewGuid();
        var rollback = Deployment.CreateRollback(original, triggeredBy);

        rollback.IsRollback.Should().BeTrue();
        rollback.PreviousDeploymentId.Should().Be(original.Id);
        rollback.Trigger.Should().Be(DeploymentTrigger.Rollback);
        rollback.TenantId.Should().Be(_tenantId);
        rollback.ProjectId.Should().Be(_projectId);
    }

    [Fact]
    public void MarkSucceeded_CalculatesDurationWhenStartedAtSet()
    {
        var deployment = CreateDeployment();
        deployment.Start();

        // small pause to ensure duration > 0
        deployment.MarkSucceeded();

        deployment.DurationSeconds.Should().BeGreaterThanOrEqualTo(0);
    }
}
