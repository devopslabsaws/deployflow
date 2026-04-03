using Cronos;
using DeployFlow.Application.Common;
using DeployFlow.Domain.Entities;
using DeployFlow.Domain.Interfaces;
using DeployFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeployFlow.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that evaluates <see cref="ScheduledTask"/> records and
/// executes them when their cron schedule is due.
///
/// Behaviour:
///   • Polls every 30 seconds.
///   • Uses the <see href="https://github.com/HangfireIO/Cronos">Cronos</see> library to
///     evaluate whether a task is overdue relative to its <see cref="ScheduledTask.LastRunAt"/>.
///   • Runs at most one instance of each task at a time (tracks running task IDs in memory).
///   • Executes the task's <see cref="ScheduledTask.Command"/> via SSH on the project's server.
///   • Records status + output via <see cref="ScheduledTask.RecordRun"/>.
///   • Supports the Cronos-recognized shortcuts: <c>@hourly</c>, <c>@daily</c>, <c>@weekly</c>,
///     <c>@monthly</c>, <c>@yearly</c>.
/// </summary>
public class ScheduledTaskRunnerService : BackgroundService
{
    private readonly IServiceProvider                        _services;
    private readonly ILogger<ScheduledTaskRunnerService>    _logger;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    // In-memory guard: prevents running the same task concurrently
    private readonly HashSet<Guid> _runningTasks = new();
    private readonly object _lock = new();

    public ScheduledTaskRunnerService(
        IServiceProvider                     services,
        ILogger<ScheduledTaskRunnerService>  logger)
    {
        _services = services;
        _logger   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScheduledTaskRunner started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EvaluateTasksAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ScheduledTaskRunner: error in evaluation loop");
            }

            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    // ── Evaluation ────────────────────────────────────────────────────────────

    private async Task EvaluateTasksAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tasks = await db.ScheduledTasks
            .Where(t => t.IsActive)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;

        foreach (var task in tasks)
        {
            if (!IsDue(task, now)) continue;

            bool alreadyRunning;
            lock (_lock)
            {
                alreadyRunning = !_runningTasks.Add(task.Id);
            }
            if (alreadyRunning) continue;

            _logger.LogInformation("ScheduledTaskRunner: task '{Name}' ({Id}) is due — executing", task.Name, task.Id);
            _ = Task.Run(() => RunTaskAsync(task.Id, ct), ct);
        }
    }

    /// <summary>Returns true if the task is overdue based on its cron expression.</summary>
    private static bool IsDue(ScheduledTask task, DateTime now)
    {
        CronExpression? expr;
        try
        {
            var frequency = NormaliseCronAlias(task.Frequency);
            expr = CronExpression.Parse(frequency, CronFormat.Standard);
        }
        catch (Exception)
        {
            return false; // unparseable cron — skip silently
        }

        var from = task.LastRunAt ?? now.AddYears(-1); // never run → treat as far in the past
        var next = expr.GetNextOccurrence(from, TimeZoneInfo.Utc);

        return next.HasValue && next.Value <= now;
    }

    /// <summary>
    /// Converts human-readable aliases to standard 5-field cron expressions so Cronos can parse them.
    /// </summary>
    private static string NormaliseCronAlias(string frequency)
        => frequency.Trim().ToLowerInvariant() switch
        {
            "daily"   or "@daily"   => "0 0 * * *",
            "hourly"  or "@hourly"  => "0 * * * *",
            "weekly"  or "@weekly"  => "0 0 * * 0",
            "monthly" or "@monthly" => "0 0 1 * *",
            "yearly"  or "@yearly"  or "@annually" => "0 0 1 1 *",
            var s => s,
        };

    // ── Execution ─────────────────────────────────────────────────────────────

    private async Task RunTaskAsync(Guid taskId, CancellationToken ct)
    {
        try
        {
            using var scope = _services.CreateScope();
            var db  = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var ssh = scope.ServiceProvider.GetRequiredService<ISshService>();
            var enc = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

            var task = await db.ScheduledTasks.FindAsync(new object[] { taskId }, ct);
            if (task is null || !task.IsActive) return;

            // Resolve server + SSH key via project
            var project = await uow.Projects.GetByIdAsync(task.ProjectId, ct);
            if (project?.ServerId is null)
            {
                _logger.LogWarning("ScheduledTaskRunner: task '{Name}' has no server assigned", task.Name);
                task.RecordRun("failed", "No server assigned to project");
                await db.SaveChangesAsync(ct);
                return;
            }

            var server = await uow.Servers.GetByIdAsync(project.ServerId.Value, ct);
            if (server?.SshKeyId is null)
            {
                task.RecordRun("failed", "No SSH key on server");
                await db.SaveChangesAsync(ct);
                return;
            }

            var sshKey     = await uow.SshKeys.GetByIdAsync(server.SshKeyId.Value, ct);
            if (sshKey is null)
            {
                task.RecordRun("failed", "SSH key record not found");
                await db.SaveChangesAsync(ct);
                return;
            }

            var privateKey = enc.Decrypt(sshKey.PrivateKeyEncrypted);

            // Build the command to execute — optionally run inside a named container
            var command = string.IsNullOrWhiteSpace(task.ContainerName)
                ? task.Command
                : $"docker exec {task.ContainerName} sh -c {ShellEscape(task.Command)}";

            // Timeout cancellation
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(task.TimeoutSeconds));

            SshCommandResult result;
            string status;
            try
            {
                result = await ssh.ExecuteCommandAsync(
                    server.IpAddress, server.SshPort, server.SshUser, privateKey, command, cts.Token);

                status = result.Success && result.ExitCode == 0 ? "success" : "failed";
            }
            catch (OperationCanceledException)
            {
                status = "timeout";
                task.RecordRun("timeout", $"Task timed out after {task.TimeoutSeconds}s");
                await db.SaveChangesAsync(ct);
                _logger.LogWarning("ScheduledTaskRunner: task '{Name}' timed out", task.Name);
                return;
            }

            var output = (result.StdOut + result.StdErr).Trim();
            task.RecordRun(status, output.Length > 0 ? output : "(no output)");
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "ScheduledTaskRunner: task '{Name}' finished with status '{Status}'",
                task.Name, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ScheduledTaskRunner: unhandled error running task {Id}", taskId);

            // Best-effort status recording
            try
            {
                using var errScope = _services.CreateScope();
                var errDb = errScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var t = await errDb.ScheduledTasks.FindAsync(new object[] { taskId }, ct);
                if (t is not null)
                {
                    t.RecordRun("failed", ex.Message);
                    await errDb.SaveChangesAsync(ct);
                }
            }
            catch { /* non-critical */ }
        }
        finally
        {
            lock (_lock) { _runningTasks.Remove(taskId); }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Single-quotes a shell argument, safely escaping embedded single quotes.</summary>
    private static string ShellEscape(string s)
        => "'" + s.Replace("'", "'\\''") + "'";
}
