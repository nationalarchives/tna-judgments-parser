#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Backlog.Tracking;

internal class TrackerDbContext(DbContextOptions<TrackerDbContext> options) : DbContext(options)
{
    public DbSet<TrackerLine> ParserEvents { get; set; }
    public DbSet<CloudwatchIngesterRunSummary> CloudwatchIngesterRunSummaries { get; set; }
    public DbSet<MarkLogicDocumentStatus> MarkLogicDocumentStatuses { get; set; }
    
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<TrackerStatus>()
            .HaveConversion<string>();
    }
}

/// <summary>
/// Used by dotnet ef migrations tool
/// </summary>
internal class TrackerDbContextFactory : IDesignTimeDbContextFactory<TrackerDbContext>
{
    public TrackerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TrackerDbContext>();
        optionsBuilder.UseSqlite("Data Source=sometracker.db");

        return new TrackerDbContext(optionsBuilder.Options);
    }
}
