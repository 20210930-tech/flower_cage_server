using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FlowerCageServer.Data;

public class FlowerCageDbContextFactory : IDesignTimeDbContextFactory<FlowerCageDbContext>
{
    public FlowerCageDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");

        var optionsBuilder = new DbContextOptionsBuilder<FlowerCageDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new FlowerCageDbContext(optionsBuilder.Options);
    }
}
