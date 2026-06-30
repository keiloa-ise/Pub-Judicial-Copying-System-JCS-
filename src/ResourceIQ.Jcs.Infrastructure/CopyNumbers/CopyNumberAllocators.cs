using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Infrastructure.Persistence;

namespace ResourceIQ.Jcs.Infrastructure.CopyNumbers;

/// <summary>
/// DEFAULT registration. Refuses to allocate because the copy-number uniqueness scope and
/// format are undefined (PRD decision #1). This deliberately fails fast rather than silently
/// committing the system to a scheme. Swap in a concrete allocator once the decision lands.
/// </summary>
public sealed class PendingCopyNumberAllocator : ICopyNumberAllocator
{
    public Task<string> AllocateAsync(Guid courtId, DateOnly reservationDate, CancellationToken ct) =>
        throw new NotSupportedException(
            "Copy-number scope/format is undefined (PRD decision #1). Register a concrete " +
            "ICopyNumberAllocator (e.g. GlobalCopyNumberAllocator) once the uniqueness scope " +
            "is confirmed, and define the matching UNIQUE constraint in CopyRequestConfiguration.");

    public Task ReleaseAsync(Guid courtId, int year, CancellationToken ct) =>
        throw new NotSupportedException("Copy-number scope is undefined (PRD decision #1).");

    public Task<int?> PeekLastAsync(Guid courtId, int year, CancellationToken ct) =>
        throw new NotSupportedException("Copy-number scope is undefined (PRD decision #1).");
}

/// <summary>
/// PER-COURT allocator (PRD decision #1, confirmed): each court has its own sequential copy
/// number. Atomically increments the court's row in <c>CourtCopyCounters</c> inside the ambient
/// create-request transaction (BR-07), so numbers never collide and reset per court. Pairs with
/// the composite UNIQUE (CourtId, CopyNumber) index.
/// </summary>
public sealed class PerCourtCopyNumberAllocator(JcsDbContext db) : ICopyNumberAllocator
{
    public async Task<string> AllocateAsync(Guid courtId, DateOnly reservationDate, CancellationToken ct)
    {
        var year = reservationDate.Year; // the case's year drives the sequence + the printed number
        var code = await db.Courts.Where(c => c.Id == courtId).Select(c => c.Code).FirstAsync(ct);

        var conn = db.Database.GetDbConnection();
        var mustClose = conn.State != ConnectionState.Open;
        if (mustClose) await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            // Per-court-per-year upsert + read, one round-trip. Safe under the create flow's
            // Serializable transaction (concurrent creates for the same court+year serialize).
            cmd.CommandText = """
                SET NOCOUNT ON;
                UPDATE CourtCopyCounters SET LastNumber = LastNumber + 1 WHERE CourtId = @c AND Year = @y;
                IF @@ROWCOUNT = 0 INSERT INTO CourtCopyCounters (CourtId, Year, LastNumber) VALUES (@c, @y, 1);
                SELECT LastNumber FROM CourtCopyCounters WHERE CourtId = @c AND Year = @y;
                """;
            void AddParam(string name, object value)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = name;
                p.Value = value;
                cmd.Parameters.Add(p);
            }
            AddParam("@c", courtId);
            AddParam("@y", year);

            var seq = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            // Copy number embeds the court code and year, e.g. C-001/2026/0001
            return $"{code}/{year}/{seq:D4}";
        }
        finally
        {
            if (mustClose) await db.Database.CloseConnectionAsync();
        }
    }

    public async Task ReleaseAsync(Guid courtId, int year, CancellationToken ct)
    {
        // FR-16: undo the last allocation for this court+year so the next create reuses the number.
        var conn = db.Database.GetDbConnection();
        var mustClose = conn.State != ConnectionState.Open;
        if (mustClose) await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            cmd.CommandText = "UPDATE CourtCopyCounters SET LastNumber = LastNumber - 1 WHERE CourtId = @c AND Year = @y AND LastNumber > 0;";
            var pc = cmd.CreateParameter(); pc.ParameterName = "@c"; pc.Value = courtId; cmd.Parameters.Add(pc);
            var py = cmd.CreateParameter(); py.ParameterName = "@y"; py.Value = year; cmd.Parameters.Add(py);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            if (mustClose) await db.Database.CloseConnectionAsync();
        }
    }

    public async Task<int?> PeekLastAsync(Guid courtId, int year, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        var mustClose = conn.State != ConnectionState.Open;
        if (mustClose) await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            cmd.CommandText = "SELECT LastNumber FROM CourtCopyCounters WHERE CourtId = @c AND Year = @y;";
            var pc = cmd.CreateParameter(); pc.ParameterName = "@c"; pc.Value = courtId; cmd.Parameters.Add(pc);
            var py = cmd.CreateParameter(); py.ParameterName = "@y"; py.Value = year; cmd.Parameters.Add(py);
            var scalar = await cmd.ExecuteScalarAsync(ct);
            return scalar is null or DBNull ? null : Convert.ToInt32(scalar);
        }
        finally
        {
            if (mustClose) await db.Database.CloseConnectionAsync();
        }
    }
}

/// <summary>
/// Alternative GLOBAL-scope allocator (single SQL Server <c>CopyNumberSequence</c>). NOT used —
/// kept for reference. The system uses <see cref="PerCourtCopyNumberAllocator"/> per decision #1.
/// </summary>
public sealed class GlobalCopyNumberAllocator(JcsDbContext db) : ICopyNumberAllocator
{
    public async Task<string> AllocateAsync(Guid courtId, DateOnly reservationDate, CancellationToken ct)
    {
        // NEXT VALUE FOR cannot live inside a derived table, so we run it as a plain scalar
        // command (not SqlQueryRaw, which wraps it). It enlists in the ambient transaction so
        // allocation is atomic with the create (BR-07).
        var conn = db.Database.GetDbConnection();
        var mustClose = conn.State != ConnectionState.Open;
        if (mustClose) await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT NEXT VALUE FOR {JcsDbContext.CopyNumberSequence}";
            cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            var scalar = await cmd.ExecuteScalarAsync(ct);
            return Convert.ToInt64(scalar).ToString("D8");
        }
        finally
        {
            if (mustClose) await db.Database.CloseConnectionAsync();
        }
    }

    public Task ReleaseAsync(Guid courtId, int year, CancellationToken ct) =>
        throw new NotSupportedException("Global SEQUENCE allocation cannot be rolled back; not used.");

    public Task<int?> PeekLastAsync(Guid courtId, int year, CancellationToken ct) =>
        throw new NotSupportedException("Global SEQUENCE allocation does not expose a per-court last; not used.");
}
