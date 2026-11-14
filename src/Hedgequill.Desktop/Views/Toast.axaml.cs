using Avalonia.Automation;
using Avalonia.Controls;

namespace Hedgequill.Desktop.Views;

public partial class Toast : UserControl
{
    private Action? _action;

    public Toast()
    {
        InitializeComponent();
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        Close.Click += (_, _) => Hide();
        ActionButton.Click += (_, _) =>
        {
            var action = _action;
            Hide();
            action?.Invoke();
        };
    }

    public string Title => Heading.Text ?? "";

    public void Show(string title, string detail, string? actionLabel = null, Action? action = null)
    {
        Heading.Text = title;
        Detail.Text = detail;
        Detail.IsVisible = detail.Length > 0;
        ActionButton.Content = actionLabel;
        ActionButton.IsVisible = actionLabel is not null;
        _action = action;
        IsVisible = true;
    }

    public void Hide()
    {
        _action = null;
        IsVisible = false;
    }
}
