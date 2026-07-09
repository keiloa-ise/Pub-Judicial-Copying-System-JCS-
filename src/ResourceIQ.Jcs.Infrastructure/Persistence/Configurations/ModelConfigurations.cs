using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourceIQ.Jcs.Domain.Entities;
using ResourceIQ.Jcs.Domain.Enums;

namespace ResourceIQ.Jcs.Infrastructure.Persistence.Configurations;

// NOTE on collation: string properties map to SQL Server `nvarchar` (Unicode) by default,
// which is required for Arabic content. The deliberate case-/accent-aware Arabic
// COLLATION must be set on the database/columns before the first migration — it is not pinned
// here so the choice is made consciously, not defaulted.

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Username).HasMaxLength(150).IsRequired();
        b.HasIndex(x => x.Username).IsUnique();
        b.Property(x => x.PasswordHash).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(200);
        b.HasMany(x => x.Courts).WithOne(uc => uc.User!).HasForeignKey(uc => uc.UserId);
    }
}

public sealed class CourtConfiguration : IEntityTypeConfiguration<Court>
{
    public void Configure(EntityTypeBuilder<Court> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();                 // FR-03
        b.Property(x => x.Name).HasMaxLength(300).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();                 // court names are unique
    }
}

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(300).IsRequired();
        b.Property(x => x.NumberingPolicy).HasConversion<int>();   // رقم المتفرق scope policy (FR-06)
        b.Property(x => x.NumberingLevel).HasMaxLength(1);          // special level A..Z (nullable)
        // رقم النسخة scope policy (FR-03). Default Room — also backfills existing rooms to room-level.
        b.Property(x => x.CopyNumberingPolicy).HasConversion<int>().HasDefaultValue(CopyNumberingPolicy.Room);
        // Room code AND name are unique WITHIN its court (confirmed business rule).
        b.HasIndex(x => new { x.CourtId, x.Code }).IsUnique();
        b.HasIndex(x => new { x.CourtId, x.Name }).IsUnique();
        b.HasOne(x => x.Court).WithMany().HasForeignKey(x => x.CourtId);
    }
}

public sealed class JudgeConfiguration : IEntityTypeConfiguration<Judge>
{
    public void Configure(EntityTypeBuilder<Judge> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(300).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();                 // judge names are unique
        b.HasMany(x => x.Rooms).WithOne(jr => jr.Judge!).HasForeignKey(jr => jr.JudgeId);
    }
}

public sealed class PanelMemberTitleConfiguration : IEntityTypeConfiguration<PanelMemberTitle>
{
    public void Configure(EntityTypeBuilder<PanelMemberTitle> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();                 // panel-member titles (صفات) are unique
    }
}

public sealed class UserCourtConfiguration : IEntityTypeConfiguration<UserCourt>
{
    public void Configure(EntityTypeBuilder<UserCourt> b)
    {
        b.HasKey(x => new { x.UserId, x.CourtId });
        b.HasOne(x => x.Court).WithMany().HasForeignKey(x => x.CourtId);
    }
}

public sealed class JudgeRoomConfiguration : IEntityTypeConfiguration<JudgeRoom>
{
    public void Configure(EntityTypeBuilder<JudgeRoom> b)
    {
        b.HasKey(x => new { x.JudgeId, x.RoomId });
        b.HasOne(x => x.Room).WithMany(r => r.Judges).HasForeignKey(x => x.RoomId);
    }
}

public sealed class CopyRequestConfiguration : IEntityTypeConfiguration<CopyRequest>
{
    public void Configure(EntityTypeBuilder<CopyRequest> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.CopyNumber).HasMaxLength(60);
        b.Property(x => x.CaseBaseNumber).HasMaxLength(100).IsRequired();
        b.Property(x => x.ExpediteRequestNumber).HasMaxLength(100); // رقم طلب الاستعجال (nullable)
        b.Property(x => x.ReferenceNumber).HasMaxLength(100);       // رقم المرجع (nullable, متفرق only)
        b.Property(x => x.State).HasConversion<int>();
        b.Property(x => x.Category).HasConversion<int>();
        b.Property(x => x.Urgency).HasConversion<int>();

        // BR-07 uniqueness — scope = PER-COURT (PRD decision #1, confirmed). The copy number is
        // sequential within each court, so the unique constraint is composite (CourtId, CopyNumber).
        b.HasIndex(x => new { x.CourtId, x.CopyNumber }).IsUnique();

        // Reporting (FR-13) — keep the grouped/filtered report queries index-served.
        b.HasIndex(x => x.CreatedUtc);
        b.HasIndex(x => x.State);
        b.HasIndex(x => x.ApprovedById);
        b.HasIndex(x => x.AssignedCopyistId);
        // BR-11: lookup of متفرق copies linked to an original copy (and the delete guard).
        b.HasIndex(x => x.OriginalCopyId);

        // رقم الأساس is unique PER COURT for عادي copies only. متفرق copies inherit the original's
        // رقم الأساس, so they are excluded via a filtered index ([Category] = 1 is Normal).
        b.HasIndex(x => new { x.CourtId, x.CaseBaseNumber }).IsUnique().HasFilter("[Category] = 1");

        b.HasOne(x => x.Content).WithOne(c => c.CopyRequest!)
            .HasForeignKey<CopyContent>(c => c.CopyRequestId);

        b.HasOne<Court>().WithMany().HasForeignKey(x => x.CourtId);
        // NoAction to avoid multiple cascade paths to CopyRequests (Court→CopyRequest and
        // Court→Room→CopyRequest both exist). A request is never deleted anyway.
        b.HasOne<Room>().WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.NoAction);
        // BR-11: self-reference متفرق → original عادي copy. NoAction; the app blocks deleting an
        // original that still has linked متفرق copies (the row is never cascade-deleted).
        b.HasOne<CopyRequest>().WithMany().HasForeignKey(x => x.OriginalCopyId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class CopyContentConfiguration : IEntityTypeConfiguration<CopyContent>
{
    public void Configure(EntityTypeBuilder<CopyContent> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.FieldValuesJson).IsRequired();
        b.Property(x => x.SectionsJson).IsRequired();        // nvarchar(max) — JSON array of sections
        b.Property(x => x.DissentSectionsJson).IsRequired().HasDefaultValue("[]"); // dissent appendix; default backfills existing rows
        b.Property(x => x.Body);                              // nvarchar(max) — legacy body
        b.HasOne(x => x.FormTemplate).WithMany().HasForeignKey(x => x.FormTemplateId);
    }
}

public sealed class FormTemplateConfiguration : IEntityTypeConfiguration<FormTemplate>
{
    public void Configure(EntityTypeBuilder<FormTemplate> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.HasMany(x => x.Fields).WithOne(f => f.FormTemplate!).HasForeignKey(f => f.FormTemplateId);
    }
}

public sealed class FormFieldConfiguration : IEntityTypeConfiguration<FormField>
{
    public void Configure(EntityTypeBuilder<FormField> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Key).HasMaxLength(100).IsRequired();
        b.Property(x => x.Label).HasMaxLength(300).IsRequired();
        b.Property(x => x.Type).HasMaxLength(50).IsRequired();
    }
}

public sealed class ParagraphTemplateConfiguration : IEntityTypeConfiguration<ParagraphTemplate>
{
    public void Configure(EntityTypeBuilder<ParagraphTemplate> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Body).IsRequired();                 // nvarchar(max) legal text
        b.HasOne(x => x.FormTemplate).WithMany().HasForeignKey(x => x.FormTemplateId);
        b.HasIndex(x => x.FormTemplateId);
    }
}

public sealed class CourtCopyCounterConfiguration : IEntityTypeConfiguration<CourtCopyCounter>
{
    public void Configure(EntityTypeBuilder<CourtCopyCounter> b)
    {
        b.HasKey(x => new { x.CourtId, x.RoomId, x.Year });
        b.HasOne(x => x.Court).WithMany().HasForeignKey(x => x.CourtId);
    }
}

public sealed class MiscNumberCounterConfiguration : IEntityTypeConfiguration<MiscNumberCounter>
{
    public void Configure(EntityTypeBuilder<MiscNumberCounter> b)
    {
        b.HasKey(x => new { x.ScopeKey, x.Year });
        b.Property(x => x.ScopeKey).HasMaxLength(80);
    }
}

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasConversion<int>();
        b.Property(x => x.ActorName).HasMaxLength(200);
        b.Property(x => x.Reason).HasMaxLength(2000);
        b.HasIndex(x => x.CopyRequestId);
        // Append-only: no cascade in, no Update/Delete path exposed. DB-level revocation of
        // UPDATE/DELETE for the app login is configured outside EF.
    }
}
