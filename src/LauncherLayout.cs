using System.Xml.Linq;

namespace Bento;

public sealed record LauncherLayout(string ProjectRoot, string Prefix, string TargetFramework)
{
    // Newest arrangement first. The launcher was flattened and renamed in the same change, but probing the
    // combinations keeps a half-applied rename buildable.
    private static readonly string[] KnownPrefixes = ["SPTushonka", "SPTarkov", "SPT"];

    /// <summary>
    /// Detects the launcher arrangement at repoPath, throwing when no known project layout is present.
    /// </summary>
    public static LauncherLayout Detect(string repoPath)
    {
        foreach (var root in new[] { repoPath, Path.Combine(repoPath, "project") })
        {
            foreach (var prefix in KnownPrefixes)
            {
                var csproj = Path.Combine(root, $"{prefix}.Launcher", $"{prefix}.Launcher.csproj");
                if (File.Exists(csproj))
                {
                    return new LauncherLayout(root, prefix, ReadTargetFramework(root, csproj));
                }
            }
        }

        throw new BentoException(
            $"No recognised launcher project layout under {repoPath}.",
            $"Expected {KnownPrefixes[0]}.Launcher. Bento targets the launcher build system from 4.1 onward."
        );
    }

    /// <summary>
    /// The launcher project, relative to the project root that dotnet runs in.
    /// </summary>
    public string CsprojRelative => Path.Combine($"{Prefix}.Launcher", $"{Prefix}.Launcher.csproj");

    // 4.1 onward keeps the TFM in Build.props beside the projects. Before that each csproj carried its own.
    private static string ReadTargetFramework(string projectRoot, string csproj)
    {
        var candidates = new[]
        {
            Path.Combine(projectRoot, "Build.props"),
            Path.Combine(projectRoot, "Directory.Build.props"),
            csproj,
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            if (
                XDocument.Load(path).Descendants("TargetFramework").FirstOrDefault()?.Value.Trim() is
                { Length: > 0 } framework
            )
            {
                return framework;
            }
        }

        throw new BentoException(
            $"No TargetFramework found for the launcher checkout at {projectRoot}.",
            "Expected one in Build.props or in the launcher csproj."
        );
    }

    /// <summary>
    /// The Build directory the launcher's SPTBuildEvent target populates.
    /// </summary>
    public string BuildDir => Path.Combine(ProjectRoot, "Build");

    /// <summary>
    /// The published Windows executable for one platform, before SPTBuildEvent copies it into Build.
    /// </summary>
    public string PublishedExe(string platform, string fileName)
    {
        return Path.Combine(
            ProjectRoot,
            $"{Prefix}.Launcher",
            "bin",
            "Release",
            TargetFramework,
            platform,
            "publish",
            fileName
        );
    }
}
