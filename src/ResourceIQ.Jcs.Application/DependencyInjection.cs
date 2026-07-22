using Microsoft.Extensions.DependencyInjection;
using ResourceIQ.Jcs.Application.Admin;
using ResourceIQ.Jcs.Application.Auth;
using ResourceIQ.Jcs.Application.CopyRequests;
using ResourceIQ.Jcs.Application.FormDrafts;
using ResourceIQ.Jcs.Application.Lookups;
using ResourceIQ.Jcs.Application.Reports;
using ResourceIQ.Jcs.Application.Review;

namespace ResourceIQ.Jcs.Application;

/// <summary>Registers the application-layer workflow services. Infrastructure registers the
/// abstractions (repositories, clock, allocator, audit writer, unit of work) separately.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddJcsApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<CreateCopyRequestService>();
        services.AddScoped<DeleteCopyService>();
        services.AddScoped<AcceptCopyService>();
        services.AddScoped<ExpediteCopyService>();
        services.AddScoped<SuspendCopyService>();
        services.AddScoped<PrintCopyService>();
        services.AddScoped<PrepareCopyService>();
        services.AddScoped<SubmitForReviewService>();
        services.AddScoped<ReviewService>();
        services.AddScoped<UnlockService>();
        services.AddScoped<CopyRequestReadService>();
        services.AddScoped<FormDraftService>();
        services.AddScoped<LookupService>();
        services.AddScoped<AdminService>();
        services.AddScoped<ReportService>();
        return services;
    }
}
