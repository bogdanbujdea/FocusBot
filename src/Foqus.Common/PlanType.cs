namespace FocusBot.WebAPI.Data.Entities;

/// <summary>The Foqus subscription tiers.</summary>
public enum PlanType
{
    /// <summary>Generic 24h trial — plan not yet chosen by the user.</summary>
    TrialFullAccess = 0,
    CloudBYOK = 1,
    CloudManaged = 2,
}