
using TextileMonitoring.Data.Entities;

namespace TextileMonitoring.Data.Repositories;

public interface ISensorDataRepository
{
    Task<List<DustSensorData>> GetDustHistoryAsync(int textileId, DateTime? start = null,
        DateTime? end = null, int limit = 100, CancellationToken ct = default);
    Task<List<FungiSensorData>> GetFungiHistoryAsync(int textileId, DateTime? start = null,
        DateTime? end = null, int limit = 100, CancellationToken ct = default);
    Task<DustSensorData?> GetLatestDustAsync(int textileId, CancellationToken ct = default);
    Task<FungiSensorData?> GetLatestFungiAsync(int textileId, CancellationToken ct = default);
    Task<int> BulkInsertDustAsync(List<DustSensorData> data, CancellationToken ct = default);
    Task<int> BulkInsertFungiAsync(List<FungiSensorData> data, CancellationToken ct = default);
}

public class SensorDataRepository : ISensorDataRepository
{
    private readonly TextileMonitoringDbContext _db;

    public SensorDataRepository(TextileMonitoringDbContext db)
    {
        _db = db;
    }

    public async Task<List<DustSensorData>> GetDustHistoryAsync(int textileId, DateTime? start = null,
        DateTime? end = null, int limit = 100, CancellationToken ct = default)
    {
        var query = _db.DustSensorData.Where(d => d.TextileId == textileId);
        if (start.HasValue) query = query.Where(d => d.ReadingTime >= start.Value);
        if (end.HasValue) query = query.Where(d => d.ReadingTime <= end.Value);
        return await query.OrderByDescending(d => d.ReadingTime).Take(limit).OrderBy(d => d.ReadingTime).ToListAsync(ct);
    }

    public async Task<List<FungiSensorData>> GetFungiHistoryAsync(int textileId, DateTime? start = null,
        DateTime? end = null, int limit = 100, CancellationToken ct = default)
    {
        var query = _db.FungiSensorData.Where(d => d.TextileId == textileId);
        if (start.HasValue) query = query.Where(d => d.ReadingTime >= start.Value);
        if (end.HasValue) query = query.Where(d => d.ReadingTime <= end.Value);
        return await query.OrderByDescending(d => d.ReadingTime).Take(limit).OrderBy(d => d.ReadingTime).ToListAsync(ct);
    }

    public async Task<DustSensorData?> GetLatestDustAsync(int textileId, CancellationToken ct = default)
    {
        return await _db.DustSensorData
            .Where(d => d.TextileId == textileId)
            .OrderByDescending(d => d.ReadingTime)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<FungiSensorData?> GetLatestFungiAsync(int textileId, CancellationToken ct = default)
    {
        return await _db.FungiSensorData
            .Where(d => d.TextileId == textileId)
            .OrderByDescending(d => d.ReadingTime)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> BulkInsertDustAsync(List<DustSensorData> data, CancellationToken ct = default)
    {
        _db.ChangeTracker.AutoDetectChangesEnabled = false;
        await _db.DustSensorData.AddRangeAsync(data, ct);
        var inserted = await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.AutoDetectChangesEnabled = true;
        return inserted;
    }

    public async Task<int> BulkInsertFungiAsync(List<FungiSensorData> data, CancellationToken ct = default)
    {
        _db.ChangeTracker.AutoDetectChangesEnabled = false;
        await _db.FungiSensorData.AddRangeAsync(data, ct);
        var inserted = await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.AutoDetectChangesEnabled = true;
        return inserted;
    }
}
