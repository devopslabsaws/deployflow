using DeployFlow.API.Controllers;
using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace DeployFlow.Tests.Application;

public class PipelineRunsSmokeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"pipeline-runs-smoke-{Guid.NewGuid()}")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static ICurrentUser CreateCurrentUser()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(x => x.TenantId).Returns(TenantId);
        currentUser.Setup(x => x.UserId).Returns(UserId);
        currentUser.Setup(x => x.Email).Returns("smoke@test.local");
        return currentUser.Object;
    }

    private static IMediator CreateMediator() => new Mock<IMediator>().Object;

    [Fact]
    public async Task PipelineRun_Start_List_Logs_Cancel_Flow_Works()
    {
        await using var db = CreateDb();

        var pipeline = new Pipeline
        {
            TenantId = TenantId,
            ProjectId = Guid.NewGuid(),
            Name = "deploy-main",
            Trigger = PipelineTriggerType.Manual,
            IsEnabled = true,
            Status = PipelineStatus.Idle,
            Stages = new List<PipelineStage>
            {
                new()
                {
                    Name = "build",
                    Order = 1,
                    Steps = new List<PipelineStep>
                    {
                        new() { Name = "compile", Type = PipelineStepType.Command },
                        new() { Name = "test", Type = PipelineStepType.Test },
                    }
                }
            }
        };

        db.Pipelines.Add(pipeline);
        await db.SaveChangesAsync();

        var pipelinesController = new PipelinesController(CreateMediator(), db, CreateCurrentUser());
        var pipelineRunsController = new PipelineRunsController(CreateMediator(), db, CreateCurrentUser());

        var startResult = await pipelinesController.StartRun(pipeline.Id, default);
        startResult.Should().BeOfType<OkObjectResult>();

        var listResult = await pipelinesController.GetRuns(pipeline.Id, default);
        var listOk = listResult.Should().BeOfType<OkObjectResult>().Subject;
        var runs = listOk.Value.Should().BeAssignableTo<IEnumerable<object>>().Subject.ToList();
        runs.Should().HaveCount(1);

        var runId = await db.PipelineRuns
            .Where(r => r.PipelineId == pipeline.Id)
            .Select(r => r.Id)
            .SingleAsync();

        var logsResult = await pipelineRunsController.GetLogs(runId, page: 1, pageSize: 50, ct: default);
        var logsOk = logsResult.Should().BeOfType<OkObjectResult>().Subject;
        logsOk.Value.Should().NotBeNull();

        var cancelResult = await pipelineRunsController.Cancel(runId, default);
        cancelResult.Should().BeOfType<OkObjectResult>();

        var run = await db.PipelineRuns.SingleAsync(r => r.Id == runId);
        run.Status.Should().Be(PipelineRunStatus.Cancelled);
        run.CompletedAt.Should().NotBeNull();

        var pipelineAfter = await db.Pipelines.SingleAsync(p => p.Id == pipeline.Id);
        pipelineAfter.Status.Should().Be(PipelineStatus.Cancelled);
    }
}
