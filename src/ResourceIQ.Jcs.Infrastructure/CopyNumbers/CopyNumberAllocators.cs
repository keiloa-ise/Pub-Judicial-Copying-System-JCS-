using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Domain.Enums;
using ResourceIQ.Jcs.Infrastructure.Persistence;

namespace ResourceIQ.Jcs.Infrastructure.CopyNumbers;

/// <summary>
/// DEFAULT registration. Refuses to allocate because the copy-number scope/format is undefined.
/// This deliberately fails fast rather than silently committing the system to a scheme. Swap in a
/// concrete allocator (e.g. <see cref="PerCourtCopyNumberAllocator"/>) once the decision lands.
/// </summary>
public sealed class PendingCopyNumberAllocator : ICopyNumberAllocator
{
    public Task<string> AllocateAsync(Guid courtId, Guid roomId, DateOnly reservationDate, CancellationToken ct) =>
        throw new NotSupportedException(
            "Copy-number scope/format is undefined. Register a concrete ICopyNumberAllocator " +
            "(e.g. PerCourtCopyNumberAllocator).");

    public Task ReleaseAsync(Guid courtId, Guid roomId, int year, CancellationToken ct) =>
        throw new NotSupportedException("Copy-number scope is undefined.");

    public Task<int?> PeekLastAsync(Guid courtId, Guid roomId, int year, CancellationToken ct) =>
        throw new NotSupportedException("Copy-number scope is undefined.");
}

/// <summary>
/// رقم النسخة allocator (FR-03, PRD decision #1). The scope is chosen per **room**:
///   • <c>CopyNumberingPolicy.Court</c> — all rooms in the court share one sequence; number is
///     <c>{courtCode}/{year}/{seq}</c>. The counter row uses <c>RoomId = Guid.Empty</c>.
///   • <c>CopyNumberingPolicy.Room</c> (default) — each room has its own sequence; number is
///     <c>{courtCode}/{roomCode}/{year}/{seq}</c> (the room code keeps numbers distinct within the
///     court, so the composite UNIQUE (CourtId, CopyNumber) index is unchanged).
/// Atomically upserts the scope's row in <c>CourtCopyCounters</c> inside the ambient create-request
/// transaction (BR-07), so numbers never collide and reset per scope per year.
/// </summary>
public sealed class PerCourtCopyNumberAllocator(JcsDbContext db) : ICopyNumberAllocator
{
    // Resolve the numbering scope (which counter row) + the codes used to format the number.
    private async Task<(Guid ScopeRoomId, string CourtCode, string? RoomCode)> ResolveScopeAsync(
        Guid roomId, CancellationToken ct)
    {
        var room = await db.Rooms.Where(r => r.Id == roomId)
            .Select(r => new { r.CopyNumberingPolicy, r.Code, CourtCode = r.Court!.Code })
            .FirstAsync(ct);
        return room.CopyNumberingPolicy == CopyNumberingPolicy.Room
            ? (roomId, room.CourtCode, room.Code)     // per-room sequence; room code in the number
            : (Guid.Empty, room.CourtCode, null);     // court-wide sequence; no room code
    }

    public async Task<string> AllocateAsync(Guid courtId, Guid roomId, DateOnly reservationDate, CancellationToken ct)
    {
        var year = reservationDate.Year; // the case's year drives the sequence + the printed number
        var (scopeRoomId, courtCode, roomCode) = await ResolveScopeAsync(roomId, ct);

        var conn = db.Database.GetDbConnection();
        var mustClose = conn.State != ConnectionState.Open;
        if (mustClose) await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            // Per-scope-per-year upsert + read, one round-trip. Safe under the create flow's
            // Serializable transaction (concurrent creates for the same scope+year serialize).
            cmd.CommandText = """
                SET NOCOUNT ON;
                UPDATE CourtCopyCounters SET LastNumber = LastNumber + 1 WHERE CourtId = @c AND RoomId = @r AND Year = @y;
                IF @@ROWCOUNT = 0 INSERT INTO CourtCopyCounters (CourtId, RoomId, Year, LastNumber) VALUES (@c, @r, @y, 1);
                SELECT LastNumber FROM CourtCopyCounters WHERE CourtId = @c AND RoomId = @r AND Year = @y;
                """;
            AddParam(cmd, "@c", courtId);
            AddParam(cmd, "@r", scopeRoomId);
            AddParam(cmd, "@y", year);

            var seq = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            // Court-level: {courtCode}/{year}/{seq}. Room-level: {courtCode}/{roomCode}/{year}/{seq}.
            return roomCode is null
                ? $"{courtCode}/{year}/{seq:D4}"
                : $"{courtCode}/{roomCode}/{year}/{seq:D4}";
        }
        finally
        {
            if (mustClose) await db.Database.CloseConnectionAsync();
        }
    }

    public async Task ReleaseAsync(Guid courtId, Guid roomId, int year, CancellationToken ct)
    {
        // FR-16: undo the last allocation for this scope+year so the next create reuses the number.
        var (scopeRoomId, _, _) = await ResolveScopeAsync(roomId, ct);
        var conn = db.Database.GetDbConnection();
        var mustClose = conn.State != ConnectionState.Open;
        if (mustClose) await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            cmd.CommandText = "UPDATE CourtCopyCounters SET LastNumber = LastNumber - 1 WHERE CourtId = @c AND RoomId = @r AND Year = @y AND LastNumber > 0;";
            AddParam(cmd, "@c", courtId);
            AddParam(cmd, "@r", scopeRoomId);
            AddParam(cmd, "@y", year);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            if (mustClose) await db.Database.CloseConnectionAsync();
        }
    }

    public async Task<int?> PeekLastAsync(Guid courtId, Guid roomId, int year, CancellationToken ct)
    {
        var (scopeRoomId, _, _) = await ResolveScopeAsync(roomId, ct);
        var conn = db.Database.GetDbConnection();
        var mustClose = conn.State != ConnectionState.Open;
        if (mustClose) await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            cmd.CommandText = "SELECT LastNumber FROM CourtCopyCounters WHERE CourtId = @c AND RoomId = @r AND Year = @y;";
            AddParam(cmd, "@c", courtId);
            AddParam(cmd, "@r", scopeRoomId);
            AddParam(cmd, "@y", year);
            var scalar = await cmd.ExecuteScalarAsync(ct);
            return scalar is null or DBNull ? null : Convert.ToInt32(scalar);
        }
        finally
        {
            if (mustClose) await db.Database.CloseConnectionAsync();
        }
    }

    private static void AddParam(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }
}
