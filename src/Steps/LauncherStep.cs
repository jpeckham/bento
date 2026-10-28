namespace Bento.Steps;

/// <summary>
/// Builds the launcher and returns its Build directory.
/// </summary>
public static class LauncherStep
{
    public const string Stage = "launcher";

    private static readonly string[] Platforms = ["win-x64", "linux-x64"];

    /// <summary>
    /// Builds the launcher and returns the Build directory its SPTBuildEvent target populates, after verifying the
    /// expected binaries and SPT_Data are in it.
    /// </summary>
    public static async Task<string> RunAsync(
        BuildContext ctx,
        BuildLogger log,
        CancellationToken cancellationToken = default
    )
    {
        var repo = ctx.Launcher!;
        var layout = LauncherLayout.Detect(repo.Path);

        var buildDir = ctx.LauncherPublishesPerRid
            ? await PublishPerRidAsync(ctx, repo, layout, log, cancellationToken)
            : await BuildWindowsOnlyAsync(layout, log, cancellationToken);

        var expected = ctx.LauncherPublishesPerRid
            ? new[] { "SPT.Launcher.exe", "SPT.Launcher.Linux" }
            : ["SPT.Launcher.exe"];
        foreach (var name in expected)
        {
            if (!File.Exists(Path.Combine(buildDir, name)))
            {
                throw new StageFailedException(
                    Stage,
                    $"Expected launcher output missing: {Path.Combine(buildDir, name)}"
                );
            }
        }

        if (!Directory.Exists(Path.Combine(buildDir, "SPT_Data")))
        {
            throw new StageFailedException(Stage, $"Expected launcher SPT_Data missing under {buildDir}.");
        }

        return buildDir;
    }

    private static async Task<string> PublishPerRidAsync(
        BuildContext ctx,
        RepoInfo repo,
        LauncherLayout layout,
        BuildLogger log,
        CancellationToken cancellationToken
    )
    {
        // Publishes one platform at a time; the publishes share obj/ and each one's SPTBuildEvent wipes Build.
        foreach (var platform in Platforms)
        {
            log.Status(Stage, $"publishing {platform} (Release)...");
            string[] arguments =
            [
                "publish",
                layout.CsprojRelative,
                "-c",
                "Release",
                "--self-contained",
                "false",
                "-f",
                layout.TargetFramework,
                "-r",
                platform,
                "-p:PublishSingleFile=true",
                $"-p:SptVersion={ctx.Version}",
                // Without this the launcher's Build.props falls back to its placeholder, shipping a fake commit hash
                // in InformationalVersion.
                $"-p:SptCommitTag={repo.Commit}",
            ];
            await RunDotnetAsync(arguments, layout.ProjectRoot, $"publish for {platform}", log, cancellationToken);
        }

        // The Linux publish wipes Build (and the Windows exe). Copy the exe back in from its own publish directory.
        var buildDir = layout.BuildDir;
        var winExe = layout.PublishedExe("win-x64", "SPT.Launcher.exe");
        if (!File.Exists(winExe))
        {
            throw new StageFailedException(Stage, $"Windows launcher exe missing: {winExe}");
        }

        Directory.CreateDirectory(buildDir);
        File.Copy(winExe, Path.Combine(buildDir, "SPT.Launcher.exe"), overwrite: true);
        return buildDir;
    }

    // Before 4.1 the launcher is Windows-only and its SPT.Build project drives everything from a PostBuild target: an
    // inner publish with the configuration and RID already fixed, then build.ps1 to populate Build. So this takes no
    // configuration, platform or version arguments, and the image needs pwsh on PATH.
    private static async Task<string> BuildWindowsOnlyAsync(
        LauncherLayout layout,
        BuildLogger log,
        CancellationToken cancellationToken
    )
    {
        log.Status(Stage, "building...");
        await RunDotnetAsync(["build"], layout.ProjectRoot, "build", log, cancellationToken);
        return layout.BuildDir;
    }

    private static async Task RunDotnetAsync(
        string[] arguments,
        string workingDirectory,
        string what,
        BuildLogger log,
        CancellationToken cancellationToken
    )
    {
        var exitCode = await ProcessRunner.RunAsync(
            "dotnet",
            arguments,
            workingDirectory,
            onLine: line => log.Line(Stage, line),
            cancellationToken: cancellationToken
        );
        if (exitCode != 0)
        {
            throw new StageFailedException(Stage, $"Launcher {what} failed (exit {exitCode}).");
        }
    }
}
