namespace FocusBot.Core.Entities;

/// <summary>
/// Result of a classification request: either a successful result or an error message.
/// </summary>
public record ClassifyAlignmentResponse(AlignmentResult? Result, string? ErrorMessage);
