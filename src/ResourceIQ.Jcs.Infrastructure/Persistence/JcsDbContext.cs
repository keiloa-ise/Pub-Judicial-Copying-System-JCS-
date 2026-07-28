using Microsoft.EntityFrameworkCore;
using ResourceIQ.Jcs.Domain.Entities;

namespace ResourceIQ.Jcs.Infrastructure.Persistence;

/// <summary>
/// EF Core 9 context (SQL Server). Schema changes ship as reviewed code-first migrations —
/// never edited out of band, never auto-applied at startup in production .
///
/// ⚠ NO migration has been generated yet on purpose: the Arabic collation (data
/// layer) and the copy-number uniqueness scope (decision #1) must be confirmed first — both
/// are expensive to change after the first migration.
/// </summary>
public sealed class JcsDbContext(DbContextOptions<JcsDbContext> options) : DbContext(options)
{
    public const string CopyNumberSequence = "CopyNumberSequence";

    public DbSet<User> Users => Set<User>();
    public DbSet<Court> Courts => Set<Court>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Judge> Judges => Set<Judge>();
    public DbSet<PanelMemberTitle> PanelMemberTitles => Set<PanelMemberTitle>();
    public DbSet<CopyRequest> CopyRequests => Set<CopyRequest>();
    public DbSet<CopyContent> CopyContents => Set<CopyContent>();
    public DbSet<CourtCopyCounter> CourtCopyCounters => Set<CourtCopyCounter>();
    public DbSet<MiscNumberCounter> MiscNumberCounters => Set<MiscNumberCounter>();
    public DbSet<FormTemplate> FormTemplates => Set<FormTemplate>();
    public DbSet<FormField> FormFields => Set<FormField>();
    public DbSet<ParagraphTemplate> ParagraphTemplates => Set<ParagraphTemplate>();
    public DbSet<FormDraft> FormDrafts => Set<FormDraft>();

    /// <summary>
    /// Audit entries. Exposed read-only on purpose — there is no public mutable DbSet and no
    /// repository Update/Delete path. Appends go through the audit writer only (invariant 4).
    /// </summary>
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <summary>
    /// PROVISIONAL Arabic collation (decision pending stakeholder confirmation). Arabic_CI_AS =
    /// case-insensitive, accent-sensitive — a sensible default for Arabic legal text, applied as
    /// the database default collation. Confirm before treating the first migration as final.
    /// </summary>
    public const string ArabicCollation = "Arabic_CI_AS";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation(ArabicCollation);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JcsDbContext).Assembly);

        // Copy-number sequence (BR-07). The UNIQUE constraint that pairs with it lives in
        // CopyRequestConfiguration. Scope is [OPEN] decision #1 — see that file.
        modelBuilder.HasSequence<long>(CopyNumberSequence).StartsAt(1).IncrementsBy(1);
    }
}
