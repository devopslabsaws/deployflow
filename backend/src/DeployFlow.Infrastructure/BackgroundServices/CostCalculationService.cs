using DeployFlow.Domain.Entities;
using DeployFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeployFlow.Infrastructure.BackgroundServices;

/// <summary>
/// Hourly background service that calculates infrastructure costs for each server
/// and resource type, writing <see cref="CostRecord"/> rows per billing period.
/// Uses cloud-approximate pricing: CPU-hour $0.048, RAM-GB-hour $0.006, Storage-GB-month $0.023.
/// </summary>
public class CostCalculationService : BackgroundService
{
    // ── Pricing constants ─────────────────────────────────────────────────────
    private const decimal CpuCoreHourUsd      = 0.048m;
    private const decimal RamGbHourUsd         = 0.006m;
    private const decimal StorageGbMonthUsd    = 0.023m;
    private const decimal DatabaseGbMonthUsd   = 0.115m;   // managed DB premium
    private const decimal ContainerHourUsd     = 0.012m;   // per active container/hour

    private readonly IServiceProvider _services;
    private readonly ILogger<CostCalculationService> _logger;

    public CostCalculationService(IServiceProvider services, ILogger<CostCalculationService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CostCalculationService started.");

        // Seed historical data once on startup; then run hourly.
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); // let migrations finish
        await SeedHistoricalDataIfEmptyAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CalculateCurrentPeriodCostsAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CostCalculationService");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken).ConfigureAwait(false);
            }
        }
    }

    // ── Seeding ───────────────────────────────────────────────────────────────

    private async Task SeedHistoricalDataIfEmptyAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (await db.CostRecords.AnyAsync(ct)) return; // already seeded

        var tenantId = await db.Tenants.Select(t => t.Id).FirstOrDefaultAsync(ct);
        if (tenantId == Guid.Empty) return;

        var servers    = await db.Servers.Where(s => s.TenantId == tenantId).ToListAsync(ct);
        var databases  = await db.Databases.Where(d => d.TenantId == tenantId).ToListAsync(ct);

        var records = new List<CostRecord>();
        var rng = new Random(42);

        // Generate 90 days of daily cost records going back 3 months
        for (int daysBack = 89; daysBack >= 0; daysBack--)
        {
            var day = DateTime.UtcNow.Date.AddDays(-daysBack);
            var period = day.ToString("yyyy-MM");

            // Server compute costs (per-day approximation: 24 CPU-hours + 24 RAM-GB-hours + storage amortised)
            foreach (var srv in servers)
            {
                int cpu   = srv.CpuCount > 0 ? srv.CpuCount : 2;
                int ram   = srv.MemoryGb  > 0 ? srv.MemoryGb  : 4;
                int disk  = srv.DiskGb    > 0 ? srv.DiskGb    : 100;

                // Add daily jitter ±8%
                decimal jitter = 1.0m + (decimal)(rng.NextDouble() * 0.16 - 0.08);
                decimal cpuDailyCost     = cpu  * 24 * CpuCoreHourUsd    * jitter;
                decimal ramDailyCost     = ram  * 24 * RamGbHourUsd      * jitter;
                decimal storageDailyCost = disk * StorageGbMonthUsd / 30m * jitter;

                records.Add(MakeCostRecord(tenantId, "server", srv.Name, srv.Id,
                    cpuDailyCost, period, day, "compute"));
                records.Add(MakeCostRecord(tenantId, "server", srv.Name, srv.Id,
                    ramDailyCost, period, day, "memory"));
                records.Add(MakeCostRecord(tenantId, "server", srv.Name, srv.Id,
                    storageDailyCost, period, day, "storage"));
            }

            // Database costs
            foreach (var db2 in databases)
            {
                int storageGb = db2.StorageGb > 0 ? db2.StorageGb : 20;
                decimal jitter = 1.0m + (decimal)(rng.NextDouble() * 0.10 - 0.05);
                decimal dbDailyCost = storageGb * DatabaseGbMonthUsd / 30m * jitter;
                records.Add(MakeCostRecord(tenantId, "database", db2.Name, db2.Id,
                    dbDailyCost, period, day, "database"));
            }

            // Container overhead (flat: containers running on servers)
            if (servers.Count > 0)
            {
                decimal jitter = 1.0m + (decimal)(rng.NextDouble() * 0.20 - 0.10);
                int estimatedContainers = 3 + rng.Next(0, servers.Count * 2);
                decimal containerDailyCost = estimatedContainers * 24 * ContainerHourUsd * jitter;
                records.Add(MakeCostRecord(tenantId, "container", "Running Containers", null,
                    containerDailyCost, period, day, "container"));
            }
        }

        db.CostRecords.AddRange(records);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {Count} historical cost records (90 days).", records.Count);
    }

    // ── Hourly calculation ────────────────────────────────────────────────────

    private async Task CalculateCurrentPeriodCostsAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tenantId = await db.Tenants.Select(t => t.Id).FirstOrDefaultAsync(ct);
        if (tenantId == Guid.Empty) return;

        var today  = DateTime.UtcNow.Date;
        var period = today.ToString("yyyy-MM");
        var rng    = new Random();

        var servers   = await db.Servers.Where(s => s.TenantId == tenantId && !s.IsDeleted).ToListAsync(ct);
        var databases = await db.Databases.Where(d => d.TenantId == tenantId && !d.IsDeleted).ToListAsync(ct);

        // Upsert today's records (delete then re-add for today to reflect current state)
        var todayRecords = db.CostRecords.Where(r =>
            r.TenantId == tenantId &&
            r.RecordedAt >= today &&
            r.RecordedAt < today.AddDays(1));
        db.CostRecords.RemoveRange(todayRecords);

        var records = new List<CostRecord>();

        foreach (var srv in servers)
        {
            int cpu  = srv.CpuCount > 0 ? srv.CpuCount : 2;
            int ram  = srv.MemoryGb  > 0 ? srv.MemoryGb  : 4;
            int disk = srv.DiskGb    > 0 ? srv.DiskGb    : 100;

            decimal jitter = 1.0m + (decimal)(rng.NextDouble() * 0.08 - 0.04);
            records.Add(MakeCostRecord(tenantId, "server", srv.Name, srv.Id,
                cpu  * 24 * CpuCoreHourUsd    * jitter, period, today, "compute"));
            records.Add(MakeCostRecord(tenantId, "server", srv.Name, srv.Id,
                ram  * 24 * RamGbHourUsd      * jitter, period, today, "memory"));
            records.Add(MakeCostRecord(tenantId, "server", srv.Name, srv.Id,
                disk * StorageGbMonthUsd / 30m * jitter, period, today, "storage"));
        }

        foreach (var db2 in databases)
        {
            int storageGb = db2.StorageGb > 0 ? db2.StorageGb : 20;
            decimal jitter = 1.0m + (decimal)(rng.NextDouble() * 0.08 - 0.04);
            records.Add(MakeCostRecord(tenantId, "database", db2.Name, db2.Id,
                storageGb * DatabaseGbMonthUsd / 30m * jitter, period, today, "database"));
        }

        if (servers.Count > 0)
        {
            decimal jitter = 1.0m + (decimal)(rng.NextDouble() * 0.08 - 0.04);
            int containers = servers.Sum(s => s.ActiveContainers > 0 ? s.ActiveContainers : 3);
            records.Add(MakeCostRecord(tenantId, "container", "Running Containers", null,
                containers * 24 * ContainerHourUsd * jitter, period, today, "container"));
        }

        db.CostRecords.AddRange(records);
        await db.SaveChangesAsync(ct);
        _logger.LogDebug("Updated {Count} cost records for {Date}.", records.Count, today.ToShortDateString());
    }

    // ── Helper ─────────────────────────────────────────────────────────────────
    // ResourceType field is used to store the billing category so no extra column is needed.
    private static CostRecord MakeCostRecord(
        Guid tenantId, string resourceType, string resourceName,
        Guid? resourceId, decimal amount, string period, DateTime recordedAt, string category) =>
        new()
        {
            TenantId     = tenantId,
            ResourceType = category,        // store category ("compute","storage"…) as ResourceType
            ResourceName = resourceName,
            ResourceId   = resourceId,
            Amount       = Math.Round(amount, 4),
            Currency     = "USD",
            Period       = period,
            RecordedAt   = recordedAt,
        };
}
