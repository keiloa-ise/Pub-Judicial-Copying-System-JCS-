using ResourceIQ.Jcs.Domain.Entities;

namespace ResourceIQ.Jcs.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct);

    /// <summary>Court ids assigned to the user (BR-06).</summary>
    Task<IReadOnlyCollection<Guid>> GetAssignedCourtIdsAsync(Guid userId, CancellationToken ct);
}
