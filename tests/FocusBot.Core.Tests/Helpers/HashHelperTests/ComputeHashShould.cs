using FocusBot.Core.Helpers;

namespace FocusBot.Core.Tests.Helpers.HashHelperTests;

public class ComputeHashShould
{
    [Fact]
    public void ReturnSameHash_WhenInputIsSame()
    {
        // Arrange
        var input = "same input";

        // Act
        var hash1 = HashHelper.ComputeHash(input);
        var hash2 = HashHelper.ComputeHash(input);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ReturnDifferentHash_WhenInputDiffers()
    {
        // Arrange
        var input1 = "input one";
        var input2 = "input two";

        // Act
        var hash1 = HashHelper.ComputeHash(input1);
        var hash2 = HashHelper.ComputeHash(input2);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Return64HexCharacters_WhenInputIsNonEmpty()
    {
        // Arrange
        var input = "any string";

        // Act
        var hash = HashHelper.ComputeHash(input);

        // Assert
        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[0-9A-Fa-f]+$");
    }

    [Fact]
    public void Return64HexCharacters_WhenInputIsEmpty()
    {
        // Arrange
        var input = string.Empty;

        // Act
        var hash = HashHelper.ComputeHash(input);

        // Assert
        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[0-9A-Fa-f]+$");
    }
}
