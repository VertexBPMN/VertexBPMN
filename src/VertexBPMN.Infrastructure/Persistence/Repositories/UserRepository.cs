using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IUserRepository.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly BpmnDbContext _db;
    public UserRepository(BpmnDbContext db) => _db = db;

    public async ValueTask<User?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        return await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }

    public async ValueTask<bool> IsInRoleAsync(string userId, string role, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(role)) return false;
        return await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .AnyAsync(u => u.Roles.Contains(role), cancellationToken);
    }

    public async IAsyncEnumerable<User> ListAsync(string? search = null, string? role = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = _db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(u => u.Username.Contains(s) || u.Email.Contains(s));
        }
        if (!string.IsNullOrWhiteSpace(role))
        {
            var r = role.Trim();
            query = query.Where(u => u.Roles.Contains(r));
        }
        await foreach (var user in query.AsNoTracking().AsAsyncEnumerable().WithCancellation(cancellationToken))
            yield return user;
    }

    public async ValueTask AddAsync(User user, CancellationToken cancellationToken = default)
    {
        if (user == null) throw new ArgumentNullException(nameof(user));
        user.CreatedAt = DateTime.UtcNow;
        user.LastModified = user.CreatedAt;
        await _db.Users.AddAsync(user, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        if (user == null) throw new ArgumentNullException(nameof(user));
        user.LastModified = DateTime.UtcNow;
        _db.Users.Update(user);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask DeleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (entity != null)
        {
            _db.Users.Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async ValueTask AssignRoleAsync(string userId, string role, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(role)) return;
        var entity = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (entity == null) return;
        if (!entity.Roles.Contains(role))
        {
            entity.Roles.Add(role);
            entity.LastModified = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async ValueTask RemoveRoleAsync(string userId, string role, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(role)) return;
        var entity = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (entity == null) return;
        if (entity.Roles.Remove(role))
        {
            entity.LastModified = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async ValueTask<IReadOnlyCollection<string>> GetRolesAsync(string userId, CancellationToken cancellationToken = default)
    {
        var roles = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Roles)
            .FirstOrDefaultAsync(cancellationToken) ?? new List<string>();
        return roles.AsReadOnly();
    }

    public async ValueTask<bool> ExistsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;
        return await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, cancellationToken);
    }
}
