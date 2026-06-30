using Microsoft.EntityFrameworkCore;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Domain.Entities;

namespace ResourceIQ.Jcs.Infrastructure.Persistence;

public sealed class UserRepository(JcsDbContext db) : IUserRepository
{
    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);

    public async Task<IReadOnlyCollection<Guid>> GetAssignedCourtIdsAsync(Guid userId, CancellationToken ct) =>
        await db.Set<UserCourt>().Where(uc => uc.UserId == userId)
            .Select(uc => uc.CourtId).ToListAsync(ct);
}
