using FocusBot.Core.Helpers;

namespace FocusBot.Core.Tests.Helpers.HashHelperTests;

public class ComputeTaskContentHashShould
{
    [Fact]
    public void ReturnSameHash_WhenDescriptionAndContextAreSame()
    {
        // Arrange
        var description = "Read work emails";
        var context = "Outlook is work";

        // Act
        var hash1 = HashHelper.ComputeTaskContentHash(description, context);
        var hash2 = HashHelper.ComputeTaskContentHash(description, context);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ReturnDifferentHash_WhenContextDiffers()
    {
        // Arrange
        var description = "Read work emails";
        var hash1 = HashHelper.ComputeTaskContentHash(description, "Outlook is work");
        var hash2 = HashHelper.ComputeTaskContentHash(description, "Gmail is work");

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ReturnDifferentHash_WhenDescriptionDiffers()
    {
        // Arrange
        var context = "Same context";
        var hash1 = HashHelper.ComputeTaskContentHash("Task A", context);
        var hash2 = HashHelper.ComputeTaskContentHash("Task B", context);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void TreatNullContextAsEmpty()
    {
        // Arrange
        var description = "Some task";

        // Act
        var hashWithNull = HashHelper.ComputeTaskContentHash(description, null);
        var hashWithEmpty = HashHelper.ComputeTaskContentHash(description, string.Empty);

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
        var hash = HashHelper.ComputeTaskContentHash(description, context);

        // Assert
        hash.Should().HaveLength(64);
        hash.Should().MatchRegex("^[0-9A-Fa-f]+$");
    }
}
