
namespace TextileMonitoring.Data.Repositories;

public interface ITextileRepository
{
    Task<Data.Entities.Textile?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<Data.Entities.Textile>> GetListAsync(string? search = null, int? status = null,
        string? dynasty = null, string? location = null, int skip = 0, int take = 50, CancellationToken ct = default);
    Task<int> GetCountAsync(string? search = null, int? status = null,
        string? dynasty = null, string? location = null, CancellationToken ct = default);
    Task<Data.Entities.Textile> CreateAsync(Data.Entities.Textile textile, CancellationToken ct = default);
    Task<Data.Entities.Textile> UpdateAsync(Data.Entities.Textile textile, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public class TextileRepository : ITextileRepository
{
    private readonly TextileMonitoringDbContext _db;

    public TextileRepository(TextileMonitoringDbContext db)
    {
        _db = db;
    }

    public async Task<Data.Entities.Textile?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.Textiles
            .Include(t => t.Sensors)
            .Include(t => t.HoleMarkers)
            .Include(t => t.MoldRegions)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<List<Data.Entities.Textile>> GetListAsync(string? search = null, int? status = null,
        string? dynasty = null, string? location = null, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var query = _db.Textiles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Name.Contains(search) || t.Description!.Contains(search));
        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(dynasty))
            query = query.Where(t => t.Dynasty == dynasty);
        if (!string.IsNullOrWhiteSpace(location))
            query = query.Where(t => t.Location == location);

        return await query
            .OrderByDescending(t => t.UpdatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<int> GetCountAsync(string? search = null, int? status = null,
        string? dynasty = null, string? location = null, CancellationToken ct = default)
    {
        var query = _db.Textiles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Name.Contains(search) || t.Description!.Contains(search));
        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(dynasty))
            query = query.Where(t => t.Dynasty == dynasty);
        if (!string.IsNullOrWhiteSpace(location))
            query = query.Where(t => t.Location == location);

        return await query.CountAsync(ct);
    }

    public async Task<Data.Entities.Textile> CreateAsync(Data.Entities.Textile textile, CancellationToken ct = default)
    {
        textile.CreatedAt = DateTime.Now;
        textile.UpdatedAt = DateTime.Now;
        _db.Textiles.Add(textile);
        await _db.SaveChangesAsync(ct);
        return textile;
    }

    public async Task<Data.Entities.Textile> UpdateAsync(Data.Entities.Textile textile, CancellationToken ct = default)
    {
        textile.UpdatedAt = DateTime.Now;
        _db.Textiles.Update(textile);
        await _db.SaveChangesAsync(ct);
        return textile;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var textile = await _db.Textiles.FindAsync(new object[] { id }, ct);
        if (textile != null)
        {
            _db.Textiles.Remove(textile);
            await _db.SaveChangesAsync(ct);
        }
    }
}
