using FluentAssertions;
using HireLens.Infrastructure.Storage;
using Xunit;

namespace HireLens.Unit.Tests;

public sealed class ObjectStorePathsTests
{
    [Fact]
    public void PhysicalPath_rejects_directory_traversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "hirelens-test-root");
        Directory.CreateDirectory(root);

        var act = () => ObjectStorePaths.PhysicalPath(root, "../outside/secret.pdf");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ResolveRoot_uses_OBJECT_STORE_ROOT_when_set()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hirelens-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("OBJECT_STORE_ROOT", dir);
        try
        {
            ObjectStorePaths.ResolveRoot(Path.GetTempPath()).Should().Be(dir);
            Directory.Exists(dir).Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("OBJECT_STORE_ROOT", null);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
