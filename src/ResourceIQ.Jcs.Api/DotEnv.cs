namespace ResourceIQ.Jcs.Api;

/// <summary>
/// Minimal, dependency-free loader for a root <c>.env</c> file (<c>KEY=VALUE</c> lines) used for
/// local development. Values are applied as process environment variables so the standard
/// configuration pipeline picks them up (e.g. <c>ConnectionStrings__Jcs</c>, <c>Jwt__SigningKey</c>,
/// <c>API_PORT</c>, <c>ASPNETCORE_ENVIRONMENT</c>).
///
/// An already-present environment variable always wins, so values injected by Docker/compose or the
/// shell are never overwritten. In containers the file simply isn't found and this is a no-op.
/// </summary>
internal static class DotEnv
{
    public static void Load()
    {
        var file = FindUpwards(".env");
        if (file is null) return;

        foreach (var raw in File.ReadAllLines(file))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            var eq = line.IndexOf('=');
            if (eq <= 0) continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();

            // Strip a single pair of surrounding quotes, if present.
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            // Never clobber a value already provided by the environment (Docker/compose/shell win).
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    // Walk up from both the working directory and the binary directory to find the repo-root .env.
    private static string? FindUpwards(string fileName)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, fileName);
                if (File.Exists(candidate)) return candidate;
            }
        }
        return null;
    }
}
