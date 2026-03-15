using Microsoft.UI.Xaml.Controls;
using FocusBot.Core;

namespace FocusBot.App.Views;

public sealed partial class HowItWorksDialog : ContentDialog
{
    public HowItWorksDialog()
    {
        InitializeComponent();
        ExtensionStoreEdgeLink.NavigateUri = ExtensionStoreLinks.EdgeAddOns;
        ExtensionStoreChromeLink.NavigateUri = ExtensionStoreLinks.ChromeWebStore;
    }
}
