
using TextileMonitoring.Data.Entities;
using TextileMonitoring.Data.Repositories;

namespace TextileMonitoring.Tests.Data;

public class RepositoryTests : TestBase
{
    [Fact]
    public async Task TextileRepository_GetByIdAsync_ReturnsCorrectEntity()
    {
        using var context = CreateDbContext(nameof(TextileRepository_GetByIdAsync_ReturnsCorrectEntity));
        var repository = new TextileRepository(context);

        var textile = new Textile
        {
            Id = 1,
            Name = "Test Textile",
            Dynasty = "明",
            Material = "云锦",
            WidthCm = 80,
            HeightCm = 120,
            Location = "A区展柜01",
            Status = 0,
            CreatedAt = DateTime.UtcNow
        };

        context.Textiles.Add(textile);
        await context.SaveChangesAsync();

        var result = await repository.GetByIdAsync(1);

        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal("Test Textile", result.Name);
        Assert.Equal("明", result.Dynasty);
    }

    [Fact]
    public async Task TextileRepository_GetByIdAsync_ReturnsNullForNotFound()
    {
        using var context = CreateDbContext(nameof(TextileRepository_GetByIdAsync_ReturnsNullForNotFound));
        var repository = new TextileRepository(context);

        var result = await repository.GetByIdAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task TextileRepository_GetAllAsync_ReturnsAllEntities()
    {
        using var context = CreateDbContext(nameof(TextileRepository_GetAllAsync_ReturnsAllEntities));
        var repository = new TextileRepository(context);

        var textiles = new[]
        {
            new Textile { Id = 1, Name = "Textile 1", Dynasty = "明", Material = "云锦", WidthCm = 80, HeightCm = 120, Location = "A区", Status = 0, CreatedAt = DateTime.UtcNow },
            new Textile { Id = 2, Name = "Textile 2", Dynasty = "清", Material = "蜀锦", WidthCm = 60, HeightCm = 90, Location = "B区", Status = 1, CreatedAt = DateTime.UtcNow },
            new Textile { Id = 3, Name = "Textile 3", Dynasty = "明", Material = "宋锦", WidthCm = 70, HeightCm = 100, Location = "C区", Status = 0, CreatedAt = DateTime.UtcNow }
        };

        context.Textiles.AddRange(textiles);
        await context.SaveChangesAsync();

        var result = await repository.GetAllAsync();

        Assert.Equal(3, result.Count);
        Assert.Contains(result, t => t.Name == "Textile 1");
        Assert.Contains(result, t => t.Name == "Textile 2");
    }

    [Fact]
    public async Task TextileRepository_GetByDynastyAsync_ReturnsFilteredEntities()
    {
        using var context = CreateDbContext(nameof(TextileRepository_GetByDynastyAsync_ReturnsFilteredEntities));
        var repository = new TextileRepository(context);

        var textiles = new[]
        {
            new Textile { Id = 1, Name = "Ming Textile", Dynasty = "明", Material = "云锦", WidthCm = 80, HeightCm = 120, Location = "A区", Status = 0, CreatedAt = DateTime.UtcNow },
            new Textile { Id = 2, Name = "Qing Textile", Dynasty = "清", Material = "蜀锦", WidthCm = 60, HeightCm = 90, Location = "B区", Status = 1, CreatedAt = DateTime.UtcNow },
            new Textile { Id = 3, Name = "Ming Textile 2", Dynasty = "明", Material = "宋锦", WidthCm = 70, HeightCm = 100, Location = "C区", Status = 0, CreatedAt = DateTime.UtcNow }
        };

        context.Textiles.AddRange(textiles);
        await context.SaveChangesAsync();

        var result = await repository.GetByDynastyAsync("明");

        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.Equal("明", t.Dynasty));
    }

    [Fact]
    public async Task TextileRepository_AddAsync_AddsEntity()
    {
        using var context = CreateDbContext(nameof(TextileRepository_AddAsync_AddsEntity));
        var repository = new TextileRepository(context);

        var textile = new Textile
        {
            Id = 1,
            Name = "New Textile",
            Dynasty = "明",
            Material = "云锦",
            WidthCm = 80,
            HeightCm = 120,
            Location = "A区展柜01",
            Status = 0,
            CreatedAt = DateTime.UtcNow
        };

        var result = await repository.AddAsync(textile);

        Assert.NotNull(result);
        Assert.Equal(1, result.Id);
        Assert.Equal(1, context.Textiles.Count());
    }

    [Fact]
    public async Task TextileRepository_UpdateAsync_UpdatesEntity()
    {
        using var context = CreateDbContext(nameof(TextileRepository_UpdateAsync_UpdatesEntity));
        var repository = new TextileRepository(context);

        var textile = new Textile
        {
            Id = 1,
            Name = "Original Name",
            Dynasty = "明",
            Material = "云锦",
            WidthCm = 80,
            HeightCm = 120,
            Location = "A区",
            Status = 0,
            CreatedAt = DateTime.UtcNow
        };

        context.Textiles.Add(textile);
        await context.SaveChangesAsync();

        textile.Name = "Updated Name";
        textile.Status = 1;

        var result = await repository.UpdateAsync(textile);

        Assert.NotNull(result);
        Assert.Equal("Updated Name", result.Name);
        Assert.Equal(1, result.Status);

        var updated = await context.Textiles.FindAsync(1);
        Assert.Equal("Updated Name", updated?.Name);
    }

    [Fact]
    public async Task TextileRepository_DeleteAsync_DeletesEntity()
    {
        using var context = CreateDbContext(nameof(TextileRepository_DeleteAsync_DeletesEntity));
        var repository = new TextileRepository(context);

        var textile = new Textile
        {
            Id = 1,
            Name = "To Delete",
            Dynasty = "明",
            Material = "云锦",
            WidthCm = 80,
            HeightCm = 120,
            Location = "A区",
            Status = 0,
            CreatedAt = DateTime.UtcNow
        };

        context.Textiles.Add(textile);
        await context.SaveChangesAsync();

        await repository.DeleteAsync(1);

        Assert.Equal(0, context.Textiles.Count());
    }

    [Fact]
    public async Task SensorDataRepository_AddRangeAsync_AddsMultipleEntities()
    {
        using var context = CreateDbContext(nameof(SensorDataRepository_AddRangeAsync_AddsMultipleEntities));
        var repository = new SensorDataRepository(context);

        var dustData = new[]
        {
            new DustSensorData { Id = 1, SensorId = 1, TextileId = 1, ReadingTime = DateTime.UtcNow, FrassDensity = 1.5, Temperature = 22.5, Humidity = 55.0, HoleCount = 2, HoleDensity = 0.5 },
            new DustSensorData { Id = 2, SensorId = 1, TextileId = 1, ReadingTime = DateTime.UtcNow.AddHours(1), FrassDensity = 1.8, Temperature = 23.0, Humidity = 56.0, HoleCount = 3, HoleDensity = 0.6 }
        };

        await repository.AddRangeAsync(dustData);

        Assert.Equal(2, context.DustSensorData.Count());
    }

    [Fact]
    public async Task SensorDataRepository_GetDustDataByTextileIdAsync_ReturnsFilteredData()
    {
        using var context = CreateDbContext(nameof(SensorDataRepository_GetDustDataByTextileIdAsync_ReturnsFilteredData));
        var repository = new SensorDataRepository(context);

        var dustData = new[]
        {
            new DustSensorData { Id = 1, SensorId = 1, TextileId = 1, ReadingTime = DateTime.UtcNow, FrassDensity = 1.5, Temperature = 22.5, Humidity = 55.0, HoleCount = 2, HoleDensity = 0.5 },
            new DustSensorData { Id = 2, SensorId = 2, TextileId = 2, ReadingTime = DateTime.UtcNow, FrassDensity = 2.5, Temperature = 23.0, Humidity = 56.0, HoleCount = 5, HoleDensity = 1.2 }
        };

        context.DustSensorData.AddRange(dustData);
        await context.SaveChangesAsync();

        var result = await repository.GetDustDataByTextileIdAsync(1, 100);

        Assert.Single(result);
        Assert.Equal(1, result.First().TextileId);
    }
}
