namespace Bento;

/// <summary>
/// The Node install a 3.x server build runs under. Each release line pins its own version in .nvmrc, so the image
/// carries several and the right one is selected per build rather than taken from PATH.
/// </summary>
public sealed record NodeToolchain(string Version, string? BinDirectory)
{
    /// <summary>
    /// The directory Node installs live in inside the image, one subdirectory per version and per major. BENTO_NODE_ROOT
    /// overrides it, which is how a dev machine with its own Node layout selects a version.
    /// </summary>
    public const string DefaultInstallRoot = "/opt/node";

    /// <summary>
    /// The install root in use, from BENTO_NODE_ROOT when set.
    /// </summary>
    public static string InstallRoot =>
        System.Environment.GetEnvironmentVariable("BENTO_NODE_ROOT") is { Length: > 0 } root
            ? root
            : DefaultInstallRoot;

    /// <summary>
    /// Reads .nvmrc from the server project directory and locates a matching Node, throwing when none is usable.
    /// </summary>
    public static NodeToolchain Resolve(string projectDir)
    {
        var version = ReadPinnedVersion(projectDir);
        var major = version[..version.IndexOf('.')];

        // The bin subdirectory is where a Unix tarball puts node; a Windows zip puts it at the root.
        string[] candidates =
        [
            Path.Combine(InstallRoot, version, "bin"),
            Path.Combine(InstallRoot, version),
            Path.Combine(InstallRoot, major, "bin"),
            Path.Combine(InstallRoot, major),
        ];
        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return new NodeToolchain(version, candidate);
            }
        }

        // Nothing installed where the image puts it, so fall back to PATH and let the caller check the major matches.
        return new NodeToolchain(version, null);
    }

    /// <summary>
    /// The environment a Node command runs with, putting the selected install ahead of everything else on PATH.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Environment()
    {
        if (BinDirectory is null)
        {
            return null;
        }

        var path = System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return new Dictionary<string, string>
        {
            ["PATH"] = path.Length == 0 ? BinDirectory : $"{BinDirectory}{Path.PathSeparator}{path}",
        };
    }

    /// <summary>
    /// The absolute path to npm. The Windows shim derives its own install location from the path it was invoked by,
    /// so launching it as a bare name with a working directory set makes it look for npm-cli.js in the wrong place.
    /// </summary>
    public string NpmPath => Executable(OperatingSystem.IsWindows() ? "npm.cmd" : "npm");

    /// <summary>
    /// The absolute path to node.
    /// </summary>
    public string NodePath => Executable(OperatingSystem.IsWindows() ? "node.exe" : "node");

    private string Executable(string fileName)
    {
        if (BinDirectory is not null)
        {
            return Path.Combine(BinDirectory, fileName);
        }

        foreach (
            var directory in (System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(
                Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries
            )
        )
        {
            var candidate = Path.Combine(directory.Trim('"'), fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new BentoException(
            $"{fileName} was not found on PATH.",
            $"Install Node {Version} under {InstallRoot}/{Version} or put it on PATH."
        );
    }

    private static string ReadPinnedVersion(string projectDir)
    {
        var nvmrc = Path.Combine(projectDir, ".nvmrc");
        if (!File.Exists(nvmrc))
        {
            throw new BentoException(
                $"No .nvmrc in {projectDir}.",
                "A 3.x server checkout pins its Node version there."
            );
        }

        var version = File.ReadAllText(nvmrc).Trim().TrimStart('v', 'V');
        if (version.Length == 0 || !version.Contains('.'))
        {
            throw new BentoException($"Could not read a Node version from {nvmrc}.");
        }

        return version;
    }
}
