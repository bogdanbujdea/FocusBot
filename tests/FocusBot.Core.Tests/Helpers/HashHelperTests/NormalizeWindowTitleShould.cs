using FocusBot.Core.Helpers;

namespace FocusBot.Core.Tests.Helpers.HashHelperTests;

public class NormalizeWindowTitleShould
{
    [Fact]
    public void ReturnEmpty_WhenTitleIsNull()
    {
        // Arrange
        string? title = null;

        // Act
        var result = HashHelper.NormalizeWindowTitle(title!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ReturnEmpty_WhenTitleIsEmpty()
    {
        // Arrange
        var title = string.Empty;

        // Act
        var result = HashHelper.NormalizeWindowTitle(title);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ReturnTitleUnchanged_WhenLengthIsWithinLimit()
    {
        // Arrange
        var title = "Short title";

        // Act
        var result = HashHelper.NormalizeWindowTitle(title);

        // Assert
        result.Should().Be(title);
    }

    [Fact]
    public void ReturnFirst200Characters_WhenTitleExceeds200Chars()
    {
        // Arrange
        var longTitle = new string('a', 250);

        // Act
        var result = HashHelper.NormalizeWindowTitle(longTitle);

        // Assert
        result.Should().HaveLength(200);
        result.Should().Be(longTitle[..200]);
    }

    [Fact]
    public void ReturnTitleUnchanged_WhenLengthIsExactly200()
    {
        // Arrange
        var title = new string('x', 200);

        // Act
        var result = HashHelper.NormalizeWindowTitle(title);

        // Assert
        result.Should().HaveLength(200);
        result.Should().Be(title);
    }
}
