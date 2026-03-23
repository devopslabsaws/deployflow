using System.Text.Json;
using DeployFlow.API.Controllers;
using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace DeployFlow.Tests.Application;

public class Sprint1ApiSmokeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"sprint1-smoke-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static ICurrentUser CreateCurrentUser()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(x => x.TenantId).Returns(TenantId);
        currentUser.Setup(x => x.UserId).Returns(UserId);
        return currentUser.Object;
    }

    private static IMediator CreateMediator() => new Mock<IMediator>().Object;

    [Fact]
    public async Task Volumes_DeleteAttachedVolume_Returns409Conflict()
    {
        await using var db = CreateDb();

        var volume = new Volume
        {
            TenantId = TenantId,
            Name = "data-volume",
            Driver = "local",
            DockerName = $"project:{Guid.NewGuid()}",
            Status = VolumeStatus.Active,
        };

        db.Volumes.Add(volume);
        await db.SaveChangesAsync();

        var controller = new VolumesController(CreateMediator(), db, CreateCurrentUser());

        var response = await controller.Delete(volume.Id, default);

        response.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Volumes_DeleteDetachedVolume_ReturnsNoContent_AndSoftDeletes()
    {
        await using var db = CreateDb();

        var volume = new Volume
        {
            TenantId = TenantId,
            Name = "cache-volume",
            Driver = "local",
            Status = VolumeStatus.Inactive,
            DockerName = null,
        };

        db.Volumes.Add(volume);
        await db.SaveChangesAsync();

        var controller = new VolumesController(CreateMediator(), db, CreateCurrentUser());

        var response = await controller.Delete(volume.Id, default);

        response.Should().BeOfType<NoContentResult>();

        var softDeleted = await db.Volumes.IgnoreQueryFilters().SingleAsync(v => v.Id == volume.Id);
        softDeleted.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Logs_GetLogs_AppliesLevelAndServiceFilters()
    {
        await using var db = CreateDb();

        var deployment = Deployment.Create(TenantId, Guid.NewGuid());
        db.Deployments.Add(deployment);

        db.DeploymentLogs.AddRange(
            new DeploymentLog
            {
                DeploymentId = deployment.Id,
                Timestamp = DateTime.UtcNow.AddSeconds(-5),
                Level = LogLevel.Info,
                Stream = "api",
                Message = "Info should be filtered out",
            },
            new DeploymentLog
            {
                DeploymentId = deployment.Id,
                Timestamp = DateTime.UtcNow.AddSeconds(-2),
                Level = LogLevel.Error,
                Stream = "api",
                Message = "Error should remain",
            },
            new DeploymentLog
            {
                DeploymentId = deployment.Id,
                Timestamp = DateTime.UtcNow.AddSeconds(-1),
                Level = LogLevel.Error,
                Stream = "worker",
                Message = "Different service should be filtered out",
            });

        await db.SaveChangesAsync();

        var controller = new LogsController(CreateMediator(), db, CreateCurrentUser());

        var response = await controller.GetLogs(level: "error", service: "api", pageSize: 200, ct: default);

        var ok = response.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);

        var items = doc.RootElement.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("Message").GetString().Should().Be("Error should remain");
        items[0].GetProperty("Level").GetString().Should().Be("error");
        items[0].GetProperty("Service").GetString().Should().Be("api");
    }

    [Fact]
    public async Task Logs_Stream_SetsSseHeaders_AndWritesHeartbeat()
    {
        await using var db = CreateDb();

        var deployment = Deployment.Create(TenantId, Guid.NewGuid());
        db.Deployments.Add(deployment);
        await db.SaveChangesAsync();

        var controller = new LogsController(CreateMediator(), db, CreateCurrentUser());

        var httpContext = new DefaultHttpContext();
        await using var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        try
        {
            await controller.Stream(ct: cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when token cancels after first loop iteration.
        }

        httpContext.Response.Headers["Content-Type"].ToString().Should().Contain("text/event-stream");
        httpContext.Response.Headers["Cache-Control"].ToString().Should().Contain("no-cache");
        httpContext.Response.Headers["Connection"].ToString().Should().Contain("keep-alive");

        responseBody.Position = 0;
        using var reader = new StreamReader(responseBody);
        var payload = await reader.ReadToEndAsync();

        payload.Should().Contain("event: heartbeat");
    }

    [Fact]
    public async Task Monitoring_Summary_UnknownServer_Returns404()
    {
        await using var db = CreateDb();
        var controller = new MonitoringController(CreateMediator(), db, CreateCurrentUser());

        var response = await controller.GetSummary(serverId: Guid.NewGuid(), range: "1h", ct: default);

        response.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Monitoring_Summary_ReturnsAveragesForTenantServerMetrics()
    {
        await using var db = CreateDb();

        var server = Server.Create(TenantId, "srv-1", "10.0.0.1");
        db.Servers.Add(server);

        db.ServerMetrics.AddRange(
            new ServerMetrics
            {
                ServerId = server.Id,
                Timestamp = DateTime.UtcNow.AddMinutes(-10),
                CpuUsagePercent = 20,
                MemoryUsageBytes = 2,
                MemoryTotalBytes = 4,
                DiskUsageBytes = 5,
                DiskTotalBytes = 10,
                NetworkRxBytes = 4 * 1024 * 1024,
                NetworkTxBytes = 2 * 1024 * 1024,
            },
            new ServerMetrics
            {
                ServerId = server.Id,
                Timestamp = DateTime.UtcNow.AddMinutes(-5),
                CpuUsagePercent = 40,
                MemoryUsageBytes = 1,
                MemoryTotalBytes = 4,
                DiskUsageBytes = 6,
                DiskTotalBytes = 10,
                NetworkRxBytes = 6 * 1024 * 1024,
                NetworkTxBytes = 4 * 1024 * 1024,
            });

        await db.SaveChangesAsync();

        var controller = new MonitoringController(CreateMediator(), db, CreateCurrentUser());

        var response = await controller.GetSummary(serverId: server.Id, range: "1h", ct: default);

        var ok = response.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("sampleCount").GetInt32().Should().Be(2);
        doc.RootElement.GetProperty("avgCpu").GetDouble().Should().Be(30);
        doc.RootElement.GetProperty("avgMemory").GetDouble().Should().Be(37.5);
        doc.RootElement.GetProperty("avgDisk").GetDouble().Should().Be(55);
        doc.RootElement.GetProperty("avgNetworkInMbps").GetDouble().Should().Be(5);
        doc.RootElement.GetProperty("avgNetworkOutMbps").GetDouble().Should().Be(3);
    }
}
