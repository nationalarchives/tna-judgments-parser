#nullable enable

using Backlog.Tracking;

using Microsoft.EntityFrameworkCore;

using Shouldly;

using Xunit;

namespace test.backlog;

internal static class TrackerDbHelper
{
    internal static TrackerDbContext OpenFileTrackerDb(string dbPath)
    {
        var dbContextOptions = new DbContextOptionsBuilder<TrackerDbContext>()
                               .UseSqlite($"Data Source={dbPath}")
                               .Options;

        return new TrackerDbContext(dbContextOptions);
    }

    internal static void SeedFileTrackerDb(string dbPath, params TrackerLine[] seedData)
    {
        using var dbContext = OpenFileTrackerDb(dbPath);
        dbContext.Database.Migrate();
        dbContext.SetupTrackerWithExistingData(seedData);
    }

    internal static TrackerDbContext CreateInMemoryTrackerDb(params TrackerLine[] seedData)
    {
        var dbContextOptions = new DbContextOptionsBuilder<TrackerDbContext>()
                               .UseSqlite("Data Source=:memory:")
                               .Options;

        var dbContext = new TrackerDbContext(dbContextOptions);

        //Ensure connection is open before we try to access it in prod code
        dbContext.Database.OpenConnection();

        if (seedData.Length > 0)
        {
            dbContext.Database.Migrate();
            dbContext.ParserEvents.AddRange(seedData);
            dbContext.SaveChanges();
        }

        return dbContext;
    }

    public static void ShouldHaveChangesSaved(this TrackerDbContext trackerDbContext)
    {
        Assert.False(trackerDbContext.ChangeTracker.HasChanges(), "Changes should have been committed to the database");
    }

    public static void SetupTrackerWithExistingData(this TrackerDbContext trackerDbContext,
        params TrackerLine[] trackerLines)
    {
        trackerDbContext.ParserEvents.AddRange(trackerLines);
        trackerDbContext.SaveChanges();
    }

    public static void ShouldHaveSavedSingleTrackerLineWhichIs(this TrackerDbContext trackerDbContext,
        TrackerLine expected)
    {
        trackerDbContext.ShouldHaveChangesSaved();
        var actualTrackerLine = trackerDbContext.ParserEvents.ShouldHaveSingleItem();
        actualTrackerLine.ShouldBe(expected);
    }

    public static void ShouldBe(this TrackerLine actual, TrackerLine expected)
    {
        actual.ShouldSatisfyAllConditions(
            t => t.Court.ShouldBe(expected.Court),
            t => t.FileExtension.ShouldBe(expected.FileExtension),
            t => t.SourceUuid.ShouldBe(expected.SourceUuid),
            t => t.ParserRunId.ShouldBe(expected.ParserRunId),
            t => t.TrackerStatus.ShouldBe(expected.TrackerStatus),
            t => t.TreReference.ShouldBe(expected.TreReference),
            t => t.Ncn.ShouldBe(expected.Ncn),
            t => t.CaseName.ShouldBe(expected.CaseName),
            t => t.OriginalFileName.ShouldBe(expected.OriginalFileName),
            t => t.DocumentContentHash.ShouldBe(expected.DocumentContentHash),
            t => t.CsvMetadataHash.ShouldBe(expected.CsvMetadataHash),
            t => t.ErrorMessage.ShouldBe(expected.ErrorMessage),
            t => t.TrackerLineLastUpdated.ShouldBe(expected.TrackerLineLastUpdated)
        );
    }
}
