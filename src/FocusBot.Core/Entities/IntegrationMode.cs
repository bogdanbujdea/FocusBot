namespace FocusBot.Core.Entities;

/// <summary>
/// Operating mode for app-extension integration.
/// </summary>
public enum IntegrationMode
{
    /// <summary>No connection to the other component; operates independently.</summary>
    Standalone,

    /// <summary>This component started the task and leads classification.</summary>
    FullMode,

    /// <summary>The other component leads; this one provides context and shows minimal UI.</summary>
    CompanionMode
}
