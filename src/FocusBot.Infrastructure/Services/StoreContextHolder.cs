using Windows.Services.Store;

namespace FocusBot.Infrastructure.Services;

/// <summary>
/// Holds the StoreContext after it has been initialized with the main window HWND.
/// Set by the app at startup (OnLaunched) so purchase UI is parented correctly.
/// </summary>
public class StoreContextHolder
{
    public StoreContext? Context { get; set; }
}
