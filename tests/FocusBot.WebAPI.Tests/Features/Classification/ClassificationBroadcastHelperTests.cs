using FocusBot.WebAPI.Features.Classification;

namespace FocusBot.WebAPI.Tests.Features.Classification;

public class ClassificationBroadcastHelperTests
{
    // ── Source derivation ─────────────────────────────────────────────────────

    [Fact]
    public void Describe_ReturnsExtensionSource_WhenUrlIsPresent()
    {
        var request = new ClassifyRequest(
            "Task", null, "msedge", "Edge - GitHub", "https://github.com/org/repo", "GitHub", null, null, null);

        var (source, _) = ClassificationBroadcastHelper.Describe(request);

        source.Should().Be("extension");
    }

    [Fact]
    public void Describe_ReturnsDesktopSource_WhenUrlIsAbsent()
    {
        var request = new ClassifyRequest(
            "Task", null, "code", "VS Code", null, null, null, null, null);

        var (source, _) = ClassificationBroadcastHelper.Describe(request);

        source.Should().Be("desktop");
    }

    // ── ActivityName derivation ───────────────────────────────────────────────

    [Fact]
    public void Describe_UsesUrl_AsActivityName_WhenUrlIsPresent()
    {
        var request = new ClassifyRequest(
            "Task", null, "msedge", "Edge - GitHub", "https://github.com/org/repo", "GitHub", null, null, null);

        var (_, activityName) = ClassificationBroadcastHelper.Describe(request);

        activityName.Should().Be("https://github.com/org/repo");
    }

    [Fact]
    public void Describe_UsesProcessName_AsActivityName_WhenNoUrl()
    {
        var request = new ClassifyRequest(
            "Task", null, "Docker Desktop", "Containers", null, null, null, null, null);

        var (_, activityName) = ClassificationBroadcastHelper.Describe(request);

        activityName.Should().Be("Docker Desktop");
    }

    [Fact]
    public void Describe_FallsBackToWindowTitle_WhenNoUrlAndNoProcessName()
    {
        var request = new ClassifyRequest(
            "Task", null, null, "My Window Title", null, null, null, null, null);

        var (_, activityName) = ClassificationBroadcastHelper.Describe(request);

        activityName.Should().Be("My Window Title");
    }

    [Fact]
    public void Describe_ReturnsEmptyString_WhenNoUrlProcessOrWindowTitle()
    {
        var request = new ClassifyRequest(
            "Task", null, null, null, null, null, null, null, null);

        var (_, activityName) = ClassificationBroadcastHelper.Describe(request);

        activityName.Should().BeEmpty();
    }

    [Fact]
    public void Describe_TrimsWhitespace_FromUrl()
    {
        var request = new ClassifyRequest(
            "Task", null, "msedge", "Edge", "  https://example.com  ", null, null, null, null);

        var (_, activityName) = ClassificationBroadcastHelper.Describe(request);

        activityName.Should().Be("https://example.com");
    }

    [Fact]
    public void Describe_TrimsWhitespace_FromProcessName()
    {
        var request = new ClassifyRequest(
            "Task", null, "  code  ", "VS Code", null, null, null, null, null);

        var (_, activityName) = ClassificationBroadcastHelper.Describe(request);

        activityName.Should().Be("code");
    }
}
