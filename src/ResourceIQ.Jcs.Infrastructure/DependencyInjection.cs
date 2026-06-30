using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Infrastructure.Audit;
using ResourceIQ.Jcs.Infrastructure.CopyNumbers;
using ResourceIQ.Jcs.Infrastructure.Persistence;
using ResourceIQ.Jcs.Infrastructure.Security;
using ResourceIQ.Jcs.Infrastructure.Time;

namespace ResourceIQ.Jcs.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddJcsInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<JcsDbContext>(opt =>
            opt.UseSqlServer(config.GetConnectionString("Jcs")));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICopyRequestRepository, CopyRequestRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped<IJcsQueries, JcsQueries>();
        services.AddScoped<IReportQueries, ReportQueries>();
        services.AddScoped<IReportExporter, Reports.ReportExporter>();
        services.AddScoped<IAdminStore, AdminStore>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, JwtTokenService>();

        // Copy-number scope = PER-COURT (PRD decision #1, confirmed). Each court has its own
        // sequence; pairs with the composite UNIQUE (CourtId, CopyNumber) index.
        services.AddScoped<ICopyNumberAllocator, PerCourtCopyNumberAllocator>();
        // رقم المتفرق: per-room for جزائية, per-court otherwise, reset yearly (FR-06).
        services.AddScoped<IMiscNumberAllocator, MiscNumberAllocator>();

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));

        return services;
    }
}
