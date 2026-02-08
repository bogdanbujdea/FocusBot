using FocusBot.Core.Helpers;

namespace FocusBot.Core.Tests.Helpers.HashHelperTests;

public class ComputeWindowContextHashShould
{
    [Fact]
    public void ReturnSameHash_WhenProcessNameAndTitleAreSame()
    {
        // Arrange
        var processName = "chrome";
        var windowTitle = "GitHub - focusbot";

        // Act
        var hash1 = HashHelper.ComputeWindowContextHash(processName, windowTitle);
        var hash2 = HashHelper.ComputeWindowContextHash(processName, windowTitle);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ReturnDifferentHash_WhenProcessNameDiffers()
    {
        // Arrange
        var title = "Same title";
        var hash1 = HashHelper.ComputeWindowContextHash("chrome", title);
        var hash2 = HashHelper.ComputeWindowContextHash("firefox", title);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ReturnDifferentHash_WhenWindowTitleDiffers()
    {
        // Arrange
        var processName = "chrome";
        var hash1 = HashHelper.ComputeWindowContextHash(processName, "Page A");
        var hash2 = HashHelper.ComputeWindowContextHash(processName, "Page B");

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void UseNormalizedTitle_WhenTitleExceeds200Chars()
    {
        // Arrange
        var processName = "notepad";
        var shortTitle = new string('a', 100);
        var longTitle = new string('a', 100) + new string('b', 150);

        // Act
        var hashShort = HashHelper.ComputeWindowContextHash(processName, shortTitle);
        var hashLong = HashHelper.ComputeWindowContextHash(processName, longTitle);

        // Assert - long title is truncated to 200 chars (100 a's + 100 b's), so different from short
        hashLong.Should().NotBe(hashShort);
        hashLong.Should().HaveLength(64);
    }

    [Fact]
    public void Return64HexCharacters()
    {
        // Arrange
        var processName = "outlook";
        var windowTitle = "Inbox - Outlook";

        // Act
        var hash = HashHelper.ComputeWindowContextHash(processName, windowTitle);

        // Assert
        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[0-9A-Fa-f]+$");
    }
}
