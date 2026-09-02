using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces.Repositories;

/// <summary>
/// Repository abstraction for identity / user data used by task assignment,
/// authorization checks and audit logging. Must be implemented using async, non-blocking I/O.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Gets a user by its technical identifier. Returns null if not found.
    /// </summary>
    ValueTask<User?> GetUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if the user is currently in the given role (direct or mapped).
    /// </summary>
    ValueTask<bool> IsInRoleAsync(string userId, string role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists users with optional filtering by search term (name/email) and/or role.
    /// </summary>
    IAsyncEnumerable<User> ListAsync(string? search = null,
                                     string? role = null,
                                     CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new user (fails if the Id already exists).
    /// </summary>
    ValueTask AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing user.
    /// </summary>
    ValueTask UpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user (idempotent).
    /// </summary>
    ValueTask DeleteAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a role to a user (no-op if already assigned).
    /// </summary>
    ValueTask AssignRoleAsync(string userId, string role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a role from a user (no-op if not assigned).
    /// </summary>
    ValueTask RemoveRoleAsync(string userId, string role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the effective roles of a user (includes direct roles only; extend if hierarchical roles are needed).
    /// </summary>
    ValueTask<IReadOnlyCollection<string>> GetRolesAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Quick existence check (used for validation paths).
    /// </summary>
    ValueTask<bool> ExistsAsync(string userId, CancellationToken cancellationToken = default);
}