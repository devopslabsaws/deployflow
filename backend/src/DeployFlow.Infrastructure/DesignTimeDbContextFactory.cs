using DeployFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DeployFlow.Infrastructure;

/// <summary>
/// Design-time factory used by EF Core tools (migrations add, migrations list, etc.)
/// Not used at runtime — the DI-registered DbContext is used instead.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "User Id=DEPLOYFLOW;Password=DeployFlow_2024!;Data Source=localhost:1521/orclpdb";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseOracle(connectionString,
                oracle => oracle.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .Options;

        return new ApplicationDbContext(options);
    }
}
