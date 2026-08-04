using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ResourceIQ.Jcs.Api.Controllers;

/// <summary>
/// Public runtime version. Prefer explicit deployment environment values, then fall back to the
/// local Git checkout when the server runs directly from a pulled repository.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/version")]
public sealed class VersionController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var env = ReadEnvironmentVersion();
        if (!string.IsNullOrWhiteSpace(env.Version) || !string.IsNullOrWhiteSpace(env.Commit))
            return Ok(env.Normalize());

        var git = ReadGitVersion();
        return Ok(git.Normalize());
    }

    private static RuntimeVersion ReadEnvironmentVersion()
    {
        var version = FirstEnv("JCS_VERSION", "APP_VERSION", "VERSION");
        var commit = FirstEnv("JCS_COMMIT", "GIT_SHA", "COMMIT_SHA");
        return new RuntimeVersion(
            Version: version,
            Commit: commit,
            Branch: FirstEnv("JCS_BRANCH", "GIT_BRANCH", "BRANCH"),
            DeployedAt: FirstEnv("JCS_DEPLOYED_AT", "BUILD_DATE", "BUILD_TIME"),
            CommitDate: null,
            Source: "environment");
    }

    private static RuntimeVersion ReadGitVersion()
    {
        var workTree = FindGitWorkTree();
        if (workTree is not null)
        {
            var commandVersion = ReadGitCommandVersion(workTree);
            if (commandVersion is not null)
                return commandVersion;
        }

        var gitDir = FindGitDirectory();
        if (gitDir is null)
            return RuntimeVersion.Unknown;

        var headPath = Path.Combine(gitDir, "HEAD");
        if (!System.IO.File.Exists(headPath))
            return RuntimeVersion.Unknown;

        var head = ReadTrimmed(headPath);
        if (string.IsNullOrWhiteSpace(head))
            return RuntimeVersion.Unknown;

        if (!head.StartsWith("ref:", StringComparison.OrdinalIgnoreCase))
        {
            return new RuntimeVersion(
                Version: BuildReadableVersion(null, head),
                Commit: head,
                Branch: "detached",
                DeployedAt: null,
                CommitDate: null,
                Source: "git");
        }

        var refName = head["ref:".Length..].Trim();
        var commit = ReadGitRef(gitDir, refName);
        var branch = refName.StartsWith("refs/heads/", StringComparison.OrdinalIgnoreCase)
            ? refName["refs/heads/".Length..]
            : refName;

        return new RuntimeVersion(
            Version: BuildReadableVersion(null, commit),
            Commit: commit,
            Branch: branch,
            DeployedAt: null,
            CommitDate: null,
            Source: "git");
    }

    private static RuntimeVersion? ReadGitCommandVersion(string workTree)
    {
        var commit = RunGit(workTree, "rev-parse", "HEAD");
        if (string.IsNullOrWhiteSpace(commit)) return null;

        var branch = RunGit(workTree, "branch", "--show-current");
        if (string.IsNullOrWhiteSpace(branch))
            branch = RunGit(workTree, "rev-parse", "--abbrev-ref", "HEAD");

        var versionDate = RunGit(workTree, "show", "-s", "--format=%cd", "--date=format:%Y.%m.%d.%H%M", "HEAD");
        var commitDate = RunGit(workTree, "show", "-s", "--format=%cI", "HEAD");

        return new RuntimeVersion(
            Version: BuildReadableVersion(versionDate, commit),
            Commit: commit,
            Branch: string.Equals(branch, "HEAD", StringComparison.OrdinalIgnoreCase) ? "detached" : branch,
            DeployedAt: null,
            CommitDate: commitDate,
            Source: "git");
    }

    private static string? FindGitWorkTree()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, ".git");
                if (Directory.Exists(candidate) || System.IO.File.Exists(candidate))
                    return dir.FullName;
            }
        }

        return null;
    }

    private static string? FindGitDirectory()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, ".git");
                if (Directory.Exists(candidate)) return candidate;

                if (System.IO.File.Exists(candidate))
                {
                    var gitDirLine = ReadTrimmed(candidate);
                    const string Prefix = "gitdir:";
                    if (string.IsNullOrWhiteSpace(gitDirLine) ||
                        !gitDirLine.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var path = gitDirLine[Prefix.Length..].Trim();
                    return Path.GetFullPath(path, dir.FullName);
                }
            }
        }

        return null;
    }

    private static string? ReadGitRef(string gitDir, string refName)
    {
        var looseRef = Path.Combine(gitDir, refName.Replace('/', Path.DirectorySeparatorChar));
        var commit = ReadTrimmed(looseRef);
        if (!string.IsNullOrWhiteSpace(commit)) return commit;

        var packedRefs = Path.Combine(gitDir, "packed-refs");
        if (!System.IO.File.Exists(packedRefs)) return null;

        foreach (var raw in System.IO.File.ReadLines(packedRefs))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#' || line[0] == '^') continue;

            var parts = line.Split(' ', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && parts[1] == refName)
                return parts[0];
        }

        return null;
    }

    private static string? ReadTrimmed(string path)
    {
        try
        {
            return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path).Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? FirstEnv(params string[] names)
    {
        foreach (var name in names)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }

        return null;
    }

    private static string? RunGit(string workTree, params string[] args)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = workTree,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (var arg in args)
                process.StartInfo.ArgumentList.Add(arg);

            process.Start();
            if (!process.WaitForExit(2000))
            {
                try { process.Kill(); } catch { /* best effort */ }
                return null;
            }

            if (process.ExitCode != 0) return null;
            var output = process.StandardOutput.ReadToEnd().Trim();
            return string.IsNullOrWhiteSpace(output) ? null : output;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildReadableVersion(string? versionDate, string? sha)
    {
        var shortSha = ShortSha(sha, 8) ?? "unknown";
        return string.IsNullOrWhiteSpace(versionDate)
            ? $"v{shortSha}"
            : $"v{versionDate.Trim()}-{shortSha}";
    }

    private static string? ShortSha(string? sha, int length = 12) =>
        string.IsNullOrWhiteSpace(sha)
            ? null
            : sha.Trim()[..Math.Min(length, sha.Trim().Length)];

    private sealed record RuntimeVersion(
        string? Version,
        string? Commit,
        string? Branch,
        string? DeployedAt,
        string? CommitDate,
        string Source)
    {
        public static readonly RuntimeVersion Unknown = new(
            Version: "development",
            Commit: null,
            Branch: null,
            DeployedAt: null,
            CommitDate: null,
            Source: "unknown");

        public RuntimeVersion Normalize()
        {
            var commit = string.IsNullOrWhiteSpace(Commit) ? null : Commit;
            return this with
            {
                Version = string.IsNullOrWhiteSpace(Version)
                    ? (commit is null ? "development" : BuildReadableVersion(DeployedAt ?? CommitDate, commit))
                    : Version,
                Commit = commit,
                Branch = string.IsNullOrWhiteSpace(Branch) ? null : Branch,
                DeployedAt = string.IsNullOrWhiteSpace(DeployedAt) ? null : DeployedAt,
                CommitDate = string.IsNullOrWhiteSpace(CommitDate) ? null : CommitDate,
            };
        }
    }
}
