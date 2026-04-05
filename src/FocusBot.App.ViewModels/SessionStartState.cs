namespace FocusBot.App.ViewModels;

public record SessionStartState
{
    public required bool IsBusy { get; init; }
    public required string? ErrorMessage { get; init; }

    public static SessionStartState Idle => new() { IsBusy = false, ErrorMessage = null };
    public static SessionStartState Loading => new() { IsBusy = true, ErrorMessage = null };
    public static SessionStartState Error(string msg) => new() { IsBusy = false, ErrorMessage = msg };
}
