using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Infrastructure.Persistence;

namespace ResourceIQ.Jcs.Infrastructure.CopyNumbers;

/// <summary>
/// Allocates رقم المتفرق for متفرق copies (FR-06). The scope is the room's configured numbering
/// policy (court / room / special-level-per-court), resolved via <c>Room.MiscScopeKey()</c>; reset
/// yearly. Atomic upsert on <c>MiscNumberCounters</c> inside the create transaction (mirrors the
/// copy-number allocator). <see cref="ReleaseAsync"/> rolls it back on delete (FR-16) so no gap.
/// </summary>
public sealed class MiscNumberAllocator(JcsDbContext db) : IMiscNumberAllocator
{
    private async Task<string> ScopeKeyAsync(Guid roomId, CancellationToken ct)
    {
        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId, ct)
                   ?? throw new InvalidOperationException($"Room not found: {roomId}");
        return room.MiscScopeKey();
    }

    public async Task<int> AllocateAsync(Guid courtId, Guid roomId, int year, CancellationToken ct)
    {
        var key = await ScopeKeyAsync(roomId, ct);
        return await ExecScalarAsync("""
            SET NOCOUNT ON;
            UPDATE MiscNumberCounters SET LastNumber = LastNumber + 1 WHERE ScopeKey = @k AND Year = @y;
            IF @@ROWCOUNT = 0 INSERT INTO MiscNumberCounters (ScopeKey, Year, LastNumber) VALUES (@k, @y, 1);
            SELECT LastNumber FROM MiscNumberCounters WHERE ScopeKey = @k AND Year = @y;
            """, key, year, ct) ?? 1;
    }

    public async Task ReleaseAsync(Guid courtId, Guid roomId, int year, CancellationToken ct)
    {
        var key = await ScopeKeyAsync(roomId, ct);
        await ExecScalarAsync(
            "UPDATE MiscNumberCounters SET LastNumber = LastNumber - 1 WHERE ScopeKey = @k AND Year = @y AND LastNumber > 0; SELECT 0;",
            key, year, ct);
    }

    public async Task<int?> PeekLastAsync(Guid courtId, Guid roomId, int year, CancellationToken ct)
    {
        var key = await ScopeKeyAsync(roomId, ct);
        return await ExecScalarAsync("SELECT LastNumber FROM MiscNumberCounters WHERE ScopeKey = @k AND Year = @y;", key, year, ct);
    }

    private async Task<int?> ExecScalarAsync(string sql, string key, int year, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        var mustClose = conn.State != ConnectionState.Open;
        if (mustClose) await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            cmd.CommandText = sql;
            AddParam(cmd, "@k", key);
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
