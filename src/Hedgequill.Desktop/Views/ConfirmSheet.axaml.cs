using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace Hedgequill.Desktop.Views;

public partial class ConfirmSheet : UserControl
{
    private Action? _confirmed;
    private Control? _returnFocus;

    public ConfirmSheet()
    {
        InitializeComponent();
        Cancel.Click += (_, _) => Hide();
        Confirm.Click += (_, _) => Accept();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
        };
    }

    public string Title => Question.Text ?? "";

    public void Show(string question, string detail, string action, Action confirmed)
    {
        Question.Text = question;
        Detail.Text = detail;
        Confirm.Content = action;
        AutomationProperties.SetName(this, question);
        _confirmed = confirmed;
        _returnFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        IsVisible = true;
        // Delete moves to Trash and can be undone, so Enter goes straight to it.
        Dispatcher.UIThread.Post(() => Confirm.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
    }

    public void Accept()
    {
        var confirmed = _confirmed;
        Hide();
        confirmed?.Invoke();
    }

    public void Hide()
    {
        _confirmed = null;
        IsVisible = false;
        _returnFocus?.Focus();
    }
}
