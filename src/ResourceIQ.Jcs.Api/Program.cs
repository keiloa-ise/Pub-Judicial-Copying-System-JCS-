using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ResourceIQ.Jcs.Api.Auth;
using ResourceIQ.Jcs.Api.Middleware;
using ResourceIQ.Jcs.Application;
using ResourceIQ.Jcs.Application.Abstractions;
using ResourceIQ.Jcs.Infrastructure;
using ResourceIQ.Jcs.Infrastructure.Security;

// Local dev: load repo-root .env (ports, connection string, JWT key) before anything reads config.
// No-op in containers / when values already come from the environment (see DotEnv).
ResourceIQ.Jcs.Api.DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

// Bind the HTTP port from configuration (API_PORT in .env) when provided, so the listen address is
// .env-driven for local runs. Falls back to launchSettings / ASPNETCORE_URLS when unset.
var apiPort = builder.Configuration["API_PORT"];
if (!string.IsNullOrWhiteSpace(apiPort))
    builder.WebHost.UseUrls($"http://localhost:{apiPort}");

// ── Layers ────────────────────────────────────────────────────────────────
builder.Services.AddJcsApplication();
builder.Services.AddJcsInfrastructure(builder.Configuration);

// Per-request current user, rebuilt from JWT claims (server-side authority).
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Server-side judgment PDF rendering (FR-15). Stateless + thread-safe → singleton.
builder.Services.AddSingleton<ResourceIQ.Jcs.Api.Pdf.JudgmentPdfService>();

// ── AuthN ─────────────────────────────────────────────────────────────────
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(string.IsNullOrEmpty(jwt.SigningKey)
                    ? new string('0', 32) // dev placeholder only; real key comes from a secret store
                    : jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        // The inline PDF view loads in an <iframe> (no Authorization header possible). For that GET
        // only, fall back to the HttpOnly "jcs_pdf" cookie. Restricted to the .../pdf path so no
        // state-changing endpoint accepts cookie auth (avoids CSRF on mutations).
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (string.IsNullOrEmpty(ctx.Token) &&
                    ctx.Request.Path.Value?.EndsWith("/pdf", StringComparison.OrdinalIgnoreCase) == true &&
                    ctx.Request.Cookies.TryGetValue("jcs_pdf", out var cookieToken) &&
                    !string.IsNullOrEmpty(cookieToken))
                {
                    ctx.Token = cookieToken;
                }
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();

// Swagger / OpenAPI (interactive endpoint explorer). Served under /api/docs so it reaches through
// the SPA's Nginx/Vite "/api" proxy unchanged. Use the "Authorize" button with a JWT from
// POST /api/auth/login to call secured endpoints.
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "JCS API", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token returned by POST /api/auth/login (without the 'Bearer ' prefix).",
    });
    o.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", doc)] = new List<string>(),
    });
});

// CORS for the Vite dev server (Arabic/RTL SPA).
const string SpaCors = "spa";
builder.Services.AddCors(o => o.AddPolicy(SpaCors, p => p
    .WithOrigins("http://localhost:5173")
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// DEVELOPMENT: apply migrations + seed demo data on startup for convenience.
// PRODUCTION: nothing happens automatically. The one-time, opt-in
// ProductionBootstrap runs migrations/seed/admin only when JCS_BOOTSTRAP=true.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<ResourceIQ.Jcs.Infrastructure.Persistence.JcsDbContext>();
    await db.Database.MigrateAsync();
    await ResourceIQ.Jcs.Infrastructure.Persistence.DbSeeder.SeedAsync(db, sp.GetRequiredService<IPasswordHasher>());
}
else
{
    await ResourceIQ.Jcs.Api.Bootstrap.ProductionBootstrap.RunAsync(app);
}

// Swagger UI at /api/docs (JSON at /api/docs/v1/swagger.json) — anonymous, proxy-friendly.
app.UseSwagger(o => o.RouteTemplate = "api/docs/{documentName}/swagger.json");
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/api/docs/v1/swagger.json", "JCS API v1");
    o.RoutePrefix = "api/docs";
    o.DocumentTitle = "JCS API — Swagger";
});

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors(SpaCors);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();
