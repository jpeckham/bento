using NUnit.Framework;

namespace Bento.Tests;

/// <summary>
/// Tests for how NodeToolchain reads the Node version a 3.x server checkout pins.
/// </summary>
[TestFixture]
public class NodeToolchainTests
{
    private string _project = null!;

    [SetUp]
    public void SetUp()
    {
        _project = Path.Combine(Path.GetTempPath(), $"bento-tests-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(_project);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_project))
        {
            Directory.Delete(_project, recursive: true);
        }
    }

    /// <summary>
    /// The pinned version is read with its leading v stripped, as .nvmrc writes it.
    /// </summary>
    [TestCase("v22.12.0", ExpectedResult = "22.12.0")]
    [TestCase("v20.11.1\n", ExpectedResult = "20.11.1")]
    [TestCase("20.11.1", ExpectedResult = "20.11.1")]
    public string ReadsPinnedVersion(string contents)
    {
        File.WriteAllText(Path.Combine(_project, ".nvmrc"), contents);
        return NodeToolchain.Resolve(_project).Version;
    }

    /// <summary>
    /// A checkout with no .nvmrc is an error rather than a build against whatever Node is on PATH.
    /// </summary>
    [Test]
    public void MissingNvmrcThrows()
    {
        Assert.That(() => NodeToolchain.Resolve(_project), Throws.TypeOf<BentoException>());
    }

    /// <summary>
    /// BENTO_NODE_ROOT points the lookup at a local Node layout, and a Windows zip puts node at the version root
    /// rather than in a bin subdirectory.
    /// </summary>
    [TestCase("22.12.0")]
    [TestCase("22")]
    public void FindsInstallUnderNodeRoot(string directory)
    {
        File.WriteAllText(Path.Combine(_project, ".nvmrc"), "v22.12.0");
        var root = Path.Combine(_project, "node-root");
        var install = Path.Combine(root, directory);
        Directory.CreateDirectory(install);

        Environment.SetEnvironmentVariable("BENTO_NODE_ROOT", root);
        try
        {
            var toolchain = NodeToolchain.Resolve(_project);

            Assert.Multiple(() =>
            {
                Assert.That(toolchain.BinDirectory, Is.EqualTo(install));
                Assert.That(toolchain.Environment()!["PATH"], Does.StartWith(install));
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("BENTO_NODE_ROOT", null);
        }
    }

    /// <summary>
    /// With no matching install the toolchain falls back to PATH, leaving the version check to the step.
    /// </summary>
    [Test]
    public void FallsBackToPathWhenNotInstalled()
    {
        File.WriteAllText(Path.Combine(_project, ".nvmrc"), "v22.12.0");

        var toolchain = NodeToolchain.Resolve(_project);

        Assert.Multiple(() =>
        {
            Assert.That(toolchain.BinDirectory, Is.Null);
            Assert.That(toolchain.Environment(), Is.Null);
        });
    }
}
