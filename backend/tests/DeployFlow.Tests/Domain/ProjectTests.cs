using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Events;
using FluentAssertions;
using Xunit;

namespace DeployFlow.Tests.Domain;

public class ProjectTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParams_ReturnsProjectWithCorrectValues()
    {
        var project = Project.Create(
            tenantId: _tenantId,
            name: "My App",
            description: "A test app",
            repositoryUrl: "https://github.com/org/repo",
            repositoryBranch: "main",
            framework: "nextjs",
            autoDeployEnabled: true);

        project.TenantId.Should().Be(_tenantId);
        project.Name.Should().Be("My App");
        project.Slug.Should().Be("my-app");
        project.Description.Should().Be("A test app");
        project.RepositoryBranch.Should().Be("main");
        project.Framework.Should().Be("nextjs");
        project.AutoDeployEnabled.Should().BeTrue();
        project.Status.Should().Be(ProjectStatus.Active);
        project.DeploymentCount.Should().Be(0);
    }

    [Theory]
    [InlineData("Hello World", "hello-world")]
    [InlineData("My--App  Name", "my-app-name")]
    [InlineData("  Leading Trailing  ", "leading-trailing")]
    public void Create_GeneratesSlugFromName(string name, string expectedSlug)
    {
        var project = Project.Create(_tenantId, name);
        project.Slug.Should().Be(expectedSlug);
    }

    [Fact]
    public void Create_RaisesProjectCreatedEvent()
    {
        var project = Project.Create(_tenantId, "Test Project");

        project.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ProjectCreatedEvent>()
            .Which.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public void Update_ChangesNameAndRegeneratesSlug()
    {
        var project = Project.Create(_tenantId, "Old Name");
        project.Update(name: "New Name");

        project.Name.Should().Be("New Name");
        project.Slug.Should().Be("new-name");
    }

    [Fact]
    public void Archive_SetsStatusToArchived()
    {
        var project = Project.Create(_tenantId, "Test");
        project.Archive();

        project.Status.Should().Be(ProjectStatus.Archived);
    }

    [Fact]
    public void Activate_SetsStatusToActive()
    {
        var project = Project.Create(_tenantId, "Test");
        project.Archive();
        project.Activate();

        project.Status.Should().Be(ProjectStatus.Active);
    }

    [Fact]
    public void AssignServer_SetsServerId()
    {
        var project = Project.Create(_tenantId, "Test");
        var serverId = Guid.NewGuid();

        project.AssignServer(serverId);

        project.ServerId.Should().Be(serverId);
    }

    [Fact]
    public void RecordDeployment_IncrementsCountAndUpdatesLastDeployment()
    {
        var project = Project.Create(_tenantId, "Test");
        var deploymentId = Guid.NewGuid();

        project.RecordDeployment(deploymentId, DeploymentStatus.Healthy);

        project.DeploymentCount.Should().Be(1);
        project.LastDeploymentId.Should().Be(deploymentId);
        project.LastDeploymentStatus.Should().Be(DeploymentStatus.Healthy);
        project.LastDeployedAt.Should().NotBeNull();
    }

    [Fact]
    public void AddTags_AddsUniqueTags()
    {
        var project = Project.Create(_tenantId, "Test", tags: new[] { "existing" });

        project.AddTags(new[] { "new-tag", "existing", "another" });

        project.Tags.Should().BeEquivalentTo(new[] { "existing", "new-tag", "another" });
    }
}
