using Avalonia.Controls;

namespace Hedgequill.Desktop.Views;

internal static class Menus
{
    /// <summary>
    /// Gives a menu fresh items each time it opens. Avalonia's popup keeps the list the menu's Items held when the popup was
    /// made, just before Opening, and Items only makes its real list on its first change. So the list is made here, before
    /// any popup, and each opening refills that same list. Setting ItemsSource in Opening, as this replaced, opened menus empty.
    /// </summary>
    public static MenuFlyout FillOnOpening(this MenuFlyout menu, Func<IEnumerable<Control>> items)
    {
        menu.Items.Clear();
        menu.Opening += (_, _) =>
        {
            menu.Items.Clear();
            foreach (var item in items())
            {
                menu.Items.Add(item);
            }
        };
        return menu;
    }
}
