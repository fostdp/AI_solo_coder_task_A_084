
using Microsoft.EntityFrameworkCore;
using TextileMonitoring.Data;

namespace TextileMonitoring.Tests;

public abstract class TestBase : IDisposable
{
    protected TextileMonitoringDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<TextileMonitoringDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var context = new TextileMonitoringDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
