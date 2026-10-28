namespace Bento;

/// <summary>
/// Everything the build steps need.
/// </summary>
public sealed class BuildContext
{
    public required bool Fresh { get; init; }
    public required SptBuildType BuildType { get; init; }
    public required string Version { get; init; }
    public string? Tag { get; init; }
    public required string OutputDir { get; init; }
    public string? ModulePackage { get; set; }

    /// <summary>
    /// The loader's version.dll built for this release's client, required for il2cpp builds.
    /// </summary>
    public string? VersionDll { get; init; }

    public required DateTime BuildTimeUtc { get; init; }
    public required ToolSet Tools { get; init; }

    public string? DevServerPath { get; init; }
    public string? DevModulesPath { get; init; }
    public string? DevLauncherPath { get; init; }

    public RepoInfo? Server { get; set; }
    public RepoInfo? Modules { get; set; }
    public RepoInfo? Launcher { get; set; }
    public string? ClientVersion { get; set; }
    public string? FreshTempRoot { get; set; }

    /// <summary>
    /// The parsed module package source (URL or directory), or null when none is set.
    /// </summary>
    public ModulePackageSource? PackageSource => ModulePackageSource.Parse(ModulePackage);

    /// <summary>
    /// 5.0 moved to the il2cpp client. It changes the static assets and how the modules build.
    /// </summary>
    public bool Il2Cpp => BuildRules.MajorOf(Version) >= 5;

    /// <summary>
    /// 3.x is the Node server, built with npm and packaged by pkg rather than published by dotnet.
    /// </summary>
    public bool NodeServer => BuildRules.MajorOf(Version) < 4;

    /// <summary>
    /// Which bundled static asset set the release is overlaid with.
    /// </summary>
    public string AssetFlavour => BuildRules.AssetFlavourFor(Version);

    // 4.1 reorganised the launcher and modules projects. Before it the launcher builds a single Windows binary with
    // its RID fixed in the csproj, and the modules take -p:Version.
    private bool PreModernProjects =>
        BuildRules.MajorOf(Version) < 4 || (BuildRules.MajorOf(Version) == 4 && BuildRules.MinorOf(Version) == 0);

    /// <summary>
    /// Whether the launcher is published once per runtime identifier rather than built once for Windows.
    /// </summary>
    public bool LauncherPublishesPerRid => !PreModernProjects;

    /// <summary>
    /// The MSBuild property the modules build takes its version from.
    /// </summary>
    public string ModulesVersionProperty => PreModernProjects ? "Version" : "SptVersion";

    /// <summary>
    /// Where the server and launcher sit inside the release tree, empty for the 3.x root layout.
    /// </summary>
    public string RuntimeFolder => BuildRules.RuntimeFolderFor(Version);

    /// <summary>
    /// The MSBuild configuration (Release or Debug) this build type compiles under.
    /// </summary>
    public string BuildConfig => BuildRules.BuildConfigFor(BuildType);

    /// <summary>
    /// The all-caps build type string passed to the builds as -p:SptBuildType.
    /// </summary>
    public string BuildTypeProperty => BuildType.ToPropertyValue();

    /// <summary>
    /// Use LZMA2 -mx=9 for releases and all fresh builds, or -mx=1 for anything else.
    /// </summary>
    public bool MaxCompression => Fresh || BuildType == SptBuildType.Release;

    /// <summary>
    /// Scratch directory under the output dir where the release tree is assembled.
    /// </summary>
    public string StagingDir => Path.Combine(OutputDir, ".staging");

    /// <summary>
    /// The assembled release tree inside the staging dir.
    /// </summary>
    public string ReleaseDir => Path.Combine(StagingDir, "release");

    /// <summary>
    /// Where the server NuGet packages are packed, under the output dir.
    /// </summary>
    public string NuGetDir => Path.Combine(OutputDir, "packages");

    /// <summary>
    /// Per-stage log directory under the output dir.
    /// </summary>
    public string LogDir => Path.Combine(OutputDir, "logs");
}
