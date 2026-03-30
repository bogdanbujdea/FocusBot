using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;

namespace FocusBot.App.Views;

public sealed partial class TrialWelcomeDialog : ContentDialog
{
    private const string BillingUrl = "https://app.foqus.me/billing";

    public TrialWelcomeDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Opens the billing page in the default browser when the user chooses View plans.
    /// </summary>
    public void OpenBillingInBrowser()
    {
        Process.Start(new ProcessStartInfo { FileName = BillingUrl, UseShellExecute = true });
    }
}
