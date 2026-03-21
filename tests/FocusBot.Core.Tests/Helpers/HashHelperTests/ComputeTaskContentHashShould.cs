using FocusBot.Core.Helpers;

namespace FocusBot.Core.Tests.Helpers.HashHelperTests;

public class ComputeSessionContentHashShould
{
    [Fact]
    public void ReturnSameHash_WhenDescriptionAndContextAreSame()
    {
        // Arrange
        var description = "Read work emails";
        var context = "Outlook is work";

        // Act
        var hash1 = HashHelper.ComputeSessionContentHash(description, context);
        var hash2 = HashHelper.ComputeSessionContentHash(description, context);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ReturnDifferentHash_WhenContextDiffers()
    {
        // Arrange
        var description = "Read work emails";
        var hash1 = HashHelper.ComputeSessionContentHash(description, "Outlook is work");
        var hash2 = HashHelper.ComputeSessionContentHash(description, "Gmail is work");

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ReturnDifferentHash_WhenDescriptionDiffers()
    {
        // Arrange
        var context = "Same context";
        var hash1 = HashHelper.ComputeSessionContentHash("Session A", context);
        var hash2 = HashHelper.ComputeSessionContentHash("Session B", context);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void TreatNullContextAsEmpty()
    {
        // Arrange
        var description = "Some task";

        // Act
        var hashWithNull = HashHelper.ComputeSessionContentHash(description, null);
        var hashWithEmpty = HashHelper.ComputeSessionContentHash(description, string.Empty);

        // Assert
        hashWithNull.Should().Be(hashWithEmpty);
    }

    [Fact]
    public void Return64HexCharacters()
    {
        // Arrange
        var description = "Read work emails";
        var context = "Outlook is my work account";

        // Act
        var hash = HashHelper.ComputeSessionContentHash(description, context);

        // Assert
        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[0-9A-Fa-f]+$");
    }
}
