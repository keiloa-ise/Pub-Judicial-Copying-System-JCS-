using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ResourceIQ.Jcs.Api.Contracts;
using ResourceIQ.Jcs.Application.Auth;

namespace ResourceIQ.Jcs.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    /// <summary>Cookie that authorizes ONLY the inline PDF view (GET .../pdf), which loads in an
    /// &lt;iframe&gt; and therefore cannot send an Authorization header. HttpOnly (not readable by JS)
    /// and scoped to the copy-request path; all other endpoints still require the Bearer header.</summary>
    private const string PdfCookie = "jcs_pdf";

    /// <summary>FR-01: log in, receive a JWT (and an HttpOnly cookie for inline PDF viewing).</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest body, CancellationToken ct)
    {
        var result = await auth.LoginAsync(new LoginCommand(body.Username, body.Password), ct);
        Response.Cookies.Append(PdfCookie, result.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,        // dev is plain HTTP; prod (HTTPS) sets Secure
            SameSite = SameSiteMode.Lax,     // same-site iframe GET — not sent cross-site
            Path = "/api/copy-requests",
            MaxAge = TimeSpan.FromHours(1),
        });
        return Ok(result);
    }

    /// <summary>Clear the PDF cookie on logout.</summary>
    [AllowAnonymous]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(PdfCookie, new CookieOptions { Path = "/api/copy-requests" });
        return NoContent();
    }
}
