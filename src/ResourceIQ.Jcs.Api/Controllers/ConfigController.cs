using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ResourceIQ.Jcs.Api.Controllers;

/// <summary>
/// Public (authenticated) feature flags the SPA needs to render role-gated UI. These mirror the
/// server-side enforcement — the server remains the authority; this only lets the client hide options
/// it isn't allowed to use. Read from .env (both default true).
/// </summary>
[ApiController]
[Authorize]
[Route("api/config")]
public sealed class ConfigController : ControllerBase
{
    private static bool Flag(string name) =>
        !string.Equals(Environment.GetEnvironmentVariable(name), "false", StringComparison.OrdinalIgnoreCase);

    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        allowCopyistReprint = Flag("ALLOW_COPYIST_REPRINT"),   // individual reprint: Copyist + Head (else Head only)
        allowHeadBatchPrint = Flag("ALLOW_HEAD_BATCH_PRINT"),  // batch-print tab: Admin + Head (else Admin only)
    });
}
