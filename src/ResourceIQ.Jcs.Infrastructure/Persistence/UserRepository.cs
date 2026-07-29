using Microsoft.EntityFrameworkCore;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Infrastructure.Persistence;

public sealed class UserRepository(JcsDbContext db) : IUserRepository
{
    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);

    public async Task<UserScope> GetAssignedScopeAsync(Guid userId, Role role, CancellationToken ct)
    {
        // Copyist/Reviewer: room-scoped. Courts are DERIVED from the courts of their rooms so any
        // remaining court-granular check/display still resolves.
        if (role is Role.Copyist or Role.Reviewer)
        {
            var roomIds = await db.Set<UserRoom>().Where(ur => ur.UserId == userId)
                .Select(ur => ur.RoomId).ToListAsync(ct);
            var courtIds = await db.Rooms.Where(r => roomIds.Contains(r.Id))
                .Select(r => r.CourtId).Distinct().ToListAsync(ct);
            return new UserScope(courtIds, roomIds);
        }

        // Registry Head: court-scoped (Administrator has no rows → unrestricted downstream).
        var courts = await db.Set<UserCourt>().Where(uc => uc.UserId == userId)
            .Select(uc => uc.CourtId).ToListAsync(ct);
        return new UserScope(courts, []);
    }
}
