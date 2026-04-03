using AutoMapper;
using DeployFlow.Application.Common;
using DeployFlow.Application.DTOs;
using DeployFlow.Application.Mappings;
using DeployFlow.Domain.Entities;
using DeployFlow.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Reflection;
using Xunit;

namespace DeployFlow.Tests.Application;

public class ProjectPortSmokeTests
{
    [Fact]
    public void Project_Record_PersistsConfiguredPort()
    {
        var tenantId = Guid.NewGuid();
        var project = Project.Create(tenantId, "todo-app", port: 3002);

        project.Port.Should().Be(3002);

        project.Update(port: 3003);
        project.Port.Should().Be(3003);
    }

    [Fact]
    public void ProjectDto_Mapping_ContainsPort()
    {
        var mapperConfig = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        var mapper = mapperConfig.CreateMapper();

        var project = Project.Create(
            tenantId: Guid.NewGuid(),
            name: "todo-app",
            repositoryUrl: "https://github.com/org/todo-app",
            repositoryBranch: "main",
            framework: "nextjs",
            port: 3002,
            autoDeployEnabled: true);

        var dto = mapper.Map<ProjectDto>(project);

        dto.Port.Should().Be(3002);
    }

    [Fact]
    public void NextDeploymentScript_UsesProjectPort3002()
    {
        var buildService = new BuildService(
            new Mock<ISshService>().Object,
            new Mock<IDockerfileGeneratorService>().Object,
            new Mock<ISmartFixEngine>().Object,
            NullLogger<BuildService>.Instance,
            Options.Create(new ProxyRoutingOptions()));

        var tenantId = Guid.NewGuid();
        var project = Project.Create(
            tenantId: tenantId,
            name: "todo-app",
            repositoryUrl: "https://github.com/org/todo-app",
            repositoryBranch: "main",
            dockerfilePath: "Dockerfile",
            framework: "nextjs",
            port: 3002,
            autoDeployEnabled: true);

        var deployment = Deployment.Create(
            tenantId: tenantId,
            projectId: project.Id,
            trigger: DeploymentTrigger.Manual,
            branch: "main");

        var method = typeof(BuildService).GetMethod("BuildDeployScript", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var script = (string)method!.Invoke(buildService, new object[]
        {
            deployment,
            project,
            new Dictionary<string, string>(),
            "10.20.30.40"
        })!;

        script.Should().Contain("DEFAULT_INTERNAL_PORT=3002");
        script.Should().Contain("HOST_PORT=\"$(allocate_host_port \"$PORT\")\"");
        script.Should().Contain("-p \"$HOST_PORT:$PORT\"");
        script.Should().Contain("http://10.20.30.40:$HOST_PORT");
    }

    [Fact]
    public void NextDeploymentScript_UsesStableProxyUrlWhenConfigured()
    {
        var buildService = new BuildService(
            new Mock<ISshService>().Object,
            new Mock<IDockerfileGeneratorService>().Object,
            new Mock<ISmartFixEngine>().Object,
            NullLogger<BuildService>.Instance,
            Options.Create(new ProxyRoutingOptions
            {
                Enabled = true,
                BaseDomain = "apps.deployflow.test",
                DockerNetwork = "deployflow-proxy",
                EntryPoints = "web,websecure",
                TlsEnabled = true,
                CertResolver = "letsencrypt"
            }));

        var tenantId = Guid.NewGuid();
        var project = Project.Create(
            tenantId: tenantId,
            name: "todo-app",
            repositoryUrl: "https://github.com/org/todo-app",
            repositoryBranch: "main",
            dockerfilePath: "Dockerfile",
            framework: "nextjs",
            port: 3002,
            autoDeployEnabled: true);

        var deployment = Deployment.Create(
            tenantId: tenantId,
            projectId: project.Id,
            trigger: DeploymentTrigger.Manual,
            branch: "main");

        var method = typeof(BuildService).GetMethod("BuildDeployScript", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var script = (string)method!.Invoke(buildService, new object[]
        {
            deployment,
            project,
            new Dictionary<string, string>(),
            "10.20.30.40"
        })!;

        script.Should().Contain("--network \"deployflow-proxy\"");
        script.Should().Contain("traefik.http.routers.deployflow-todo-app.rule=Host(`todo-app.apps.deployflow.test`)");
        script.Should().Contain("traefik.http.services.deployflow-todo-app.loadbalancer.server.port=$PORT");
        script.Should().Contain("DEPLOYFLOW_URL=https://todo-app.apps.deployflow.test");
    }

    [Fact]
    public void BlueGreenBuildScript_AllocatesDynamicHostPort()
    {
        var method = typeof(BlueGreenDeploymentService).GetMethod("BuildScript", BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var project = Project.Create(
            tenantId: Guid.NewGuid(),
            name: "todo-app",
            repositoryUrl: "https://github.com/org/todo-app",
            repositoryBranch: "main",
            dockerfilePath: "Dockerfile",
            framework: "nextjs",
            port: 3002,
            autoDeployEnabled: true);

        var script = (string)method!.Invoke(null, new object[]
        {
            project,
            "deployflow/todo-app:green-deadbeef",
            "todo-app-green",
            string.Empty
        })!;

        script.Should().Contain("DEFAULT_INTERNAL_PORT=3002");
        script.Should().Contain("HOST_PORT=\"$(allocate_host_port \"$PORT\")\"");
        script.Should().Contain("-p \"$HOST_PORT:$PORT\"");
        script.Should().Contain("DEPLOYFLOW_SLOT_PORT=$HOST_PORT");
    }
}
