using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using ConsoleManager.Models;
using System.Windows.Shapes;

namespace ConsoleManager
{
    public static class VisualTreeHelperExtensions
    {
        public static IEnumerable<T> FindVisualChildren<T>(this DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                        yield return (T)child;

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                        yield return childOfChild;
                }
            }
        }
    }

    public static class Utilities
    {
        public static ColorSettings colorSettings = new();
        public static bool darkMode = true;

        public delegate void ConsoleListOperation(UIElement element);
        public delegate void VisibilityOperation(Visibility visibility);
        public delegate void TextOperation(string text);
        public delegate void FocusOperation();
        public delegate int CountOperation();
        public static MouseButtonEventHandler ColorBoxMouseDownHandler { get; set; }
        public static ConsoleListOperation RemoveFromConsoleList { get; set; }
        public static ConsoleListOperation AddToConsoleList { get; set; }
        public static VisibilityOperation SetEmptyStateVisibility { get; set; }
        public static VisibilityOperation SetRenameOverlayVisibility { get; set; }
        public static TextOperation SetRenameTextBoxText { get; set; }
        public static FocusOperation FocusRenameTextBox { get; set; }
        public static Action SelectAllRenameTextBox { get; set; }
        public static CountOperation GetConsoleListChildrenCount { get; set; }

        public static Border currentConsoleToRename;

        // Add new delegates for resource operations
        public delegate object FindResourceOperation(string resourceKey);
        public delegate void ConsoleItemClickOperation(Border clickedItem);
        public delegate void ConnectOperation(string ip);
        public delegate void InjectOperation(string ip);
        public delegate void RemoveConsoleOperation(string ip);

        // Add new delegate for attach operation
        public delegate void AttachOperation(string ip);

        // Add new delegate for unload operation
        public delegate void UnloadOperation(string ip);

        // Add new delegate for disconnect operation
        public delegate void DisconnectOperation(string ip);

        // Add new properties to hold the delegates
        public static FindResourceOperation FindResource { get; set; }
        public static ConsoleItemClickOperation HandleConsoleItemClick { get; set; }
        public static ConnectOperation ConnectToConsole { get; set; }
        public static InjectOperation InjectPayload { get; set; }
        public static RemoveConsoleOperation RemoveConsole { get; set; }
        public static DisconnectOperation DisconnectFromConsole { get; set; }
        public static AttachOperation AttachToConsole { get; set; }
        public static UnloadOperation UnloadPayload { get; set; }

        public static void UpdateResourceColor(string resourceKey, string colorHex)
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            Application.Current.Resources[resourceKey] = color;

            if (resourceKey == "PrimaryColor")
            {
                var darkerColor = Color.FromRgb(
                    (byte)(color.R * 0.67),
                    (byte)(color.G * 0.67),
                    (byte)(color.B * 0.67)
                );

                var gradientBrush = new LinearGradientBrush(
                    color,
                    darkerColor,
                    new Point(0, 0),
                    new Point(0, 1)
                );

                Application.Current.Resources["PrimaryGradient"] = gradientBrush;
                Application.Current.Resources["PrimaryBrush"] = gradientBrush;
                Application.Current.Resources["ColorPrimary"] = gradientBrush;
            }
            else
                Application.Current.Resources[$"Color{resourceKey.Replace("Color", "")}"] = new SolidColorBrush(color);
        }

        public static void UpdateColorPreview(string colorName, string hexCode)
        {
            try
            {
                var colorBoxes = Application.Current.MainWindow.FindVisualChildren<Border>()
                    .Where(b => b.Width == 24 && b.Height == 24 && b.Parent is Grid);

                foreach (var box in colorBoxes)
                {
                    var grid = box.Parent as Grid;
                    if (grid == null) continue;

                    var descriptionText = grid.Children.OfType<TextBlock>()
                        .LastOrDefault()?.Text?.ToLower() ?? "";

                    bool shouldUpdate = false;
                    switch (colorName)
                    {
                        case "PrimaryColor":
                            shouldUpdate = descriptionText.Contains("primary color");
                            break;
                        case "BackgroundColor":
                            shouldUpdate = descriptionText.Contains("background color");
                            break;
                        case "CardColor":
                            shouldUpdate = descriptionText.Contains("cards and panels");
                            break;
                        case "ShadowColor":
                            shouldUpdate = descriptionText.Contains("darkest elements");
                            break;
                        case "GrayColor":
                            shouldUpdate = descriptionText.Contains("borders and separators");
                            break;
                        case "TextColor":
                            shouldUpdate = descriptionText.Contains("primary text color");
                            break;
                        case "TextSecondaryColor":
                            shouldUpdate = descriptionText.Contains("less prominent text");
                            break;
                    }

                    if (shouldUpdate)
                    {
                        box.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexCode));

                        box.MouseDown -= ColorBoxMouseDownHandler;
                        box.MouseDown += ColorBoxMouseDownHandler;

                        var hexText = grid.Children.OfType<TextBlock>()
                            .FirstOrDefault(t => t.Text?.StartsWith("#") == true);
                        if (hexText != null)
                            hexText.Text = hexCode.ToUpper();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating color preview: {ex.Message}");
            }
        }

        public static void ApplyColorSettings()
        {
            try
            {
                UpdateResourceColor("PrimaryColor", colorSettings.PrimaryColor);
                UpdateResourceColor("GrayColor", colorSettings.GrayColor);
                UpdateResourceColor("TextColor", colorSettings.TextColor);
                UpdateResourceColor("DarkColor", colorSettings.BackgroundColor);
                UpdateResourceColor("DarkerColor", colorSettings.CardColor);
                UpdateResourceColor("DarkestColor", colorSettings.ShadowColor);
                UpdateResourceColor("TextSecondaryColor", colorSettings.TextSecondaryColor);

                UpdateColorPreview("PrimaryColor", colorSettings.PrimaryColor);
                UpdateColorPreview("BackgroundColor", colorSettings.BackgroundColor);
                UpdateColorPreview("CardColor", colorSettings.CardColor);
                UpdateColorPreview("ShadowColor", colorSettings.ShadowColor);
                UpdateColorPreview("GrayColor", colorSettings.GrayColor);
                UpdateColorPreview("TextColor", colorSettings.TextColor);
                UpdateColorPreview("TextSecondaryColor", colorSettings.TextSecondaryColor);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying color settings: {ex.Message}");
            }
        }

        public static string ColorToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        public static void SaveColorSettings()
        {
            try
            {
                var resources = Application.Current.Resources;

                colorSettings.IsDarkMode = darkMode;
                colorSettings.PrimaryColor = ColorToHex((Color)resources["PrimaryColor"]);
                colorSettings.GrayColor = ColorToHex((Color)resources["GrayColor"]);
                colorSettings.TextColor = ColorToHex((Color)resources["TextColor"]);
                colorSettings.BackgroundColor = ColorToHex((Color)resources["DarkColor"]);
                colorSettings.CardColor = ColorToHex((Color)resources["DarkerColor"]);
                colorSettings.ShadowColor = ColorToHex((Color)resources["DarkestColor"]);
                colorSettings.TextSecondaryColor = ColorToHex((Color)resources["TextSecondaryColor"]);

                colorSettings.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving color settings: {ex.Message}");
            }
        }

        public static void DeleteConsole(Border consoleItem)
        {
            if (consoleItem != null)
            {
                RemoveFromConsoleList?.Invoke(consoleItem);

                if (GetConsoleListChildrenCount?.Invoke() == 0)
                    SetEmptyStateVisibility?.Invoke(Visibility.Visible);
            }
        }

        public static void ShowRenameOverlay(Border consoleItem)
        {
            currentConsoleToRename = consoleItem;

            var textStack = consoleItem.Child as Grid;
            var consoleName = textStack?.Children.OfType<StackPanel>().FirstOrDefault()
                ?.Children.OfType<TextBlock>().FirstOrDefault()?.Text ?? "";

            SetRenameTextBoxText?.Invoke(consoleName);
            SetRenameOverlayVisibility?.Invoke(Visibility.Visible);
            FocusRenameTextBox?.Invoke();
            SelectAllRenameTextBox?.Invoke();
        }

        private static string GetConsoleIP(Border consoleItem)
        {
            var grid = consoleItem.Child as Grid;
            var stackPanel = grid?.Children.OfType<StackPanel>().FirstOrDefault();
            return stackPanel?.Children.OfType<TextBlock>().LastOrDefault()?.Text;
        }

        private static MenuItem CreateMenuItem(string header, string style, Action<Border> clickHandler)
        {
            var item = new MenuItem
            {
                Header = header,
                Style = (Style)FindResource?.Invoke(style)
            };

            item.Click += (s, args) =>
            {
                if (s is MenuItem menuItem &&
                    menuItem.Parent is ContextMenu cm &&
                    cm.PlacementTarget is Border border)
                {
                    clickHandler(border);
                }
            };

            return item;
        }

        public static ContextMenu CreateConsoleContextMenu(Border consoleItem)
        {
            var menu = new ContextMenu { Style = FindResource?.Invoke("DarkContextMenu") as Style };

            menu.Items.Add(CreateMenuItem("Inject", "DarkMenuItem",
                border => InjectPayload?.Invoke(GetConsoleIP(border))));

            menu.Items.Add(CreateMenuItem("Unload", "DarkMenuItem",
                border => UnloadPayload?.Invoke(GetConsoleIP(border))));

            menu.Items.Add(new Separator { Style = FindResource?.Invoke("MenuSeparator") as Style });

            menu.Items.Add(CreateMenuItem("Connect", "DarkMenuItem",
                border => ConnectToConsole?.Invoke(GetConsoleIP(border))));

            menu.Items.Add(CreateMenuItem("Disconnect", "DarkMenuItem",
                border => DisconnectFromConsole?.Invoke(GetConsoleIP(border))));

            menu.Items.Add(CreateMenuItem("Attach", "DarkMenuItem",
                border => AttachToConsole?.Invoke(GetConsoleIP(border))));

            menu.Items.Add(new Separator { Style = FindResource?.Invoke("MenuSeparator") as Style });

            menu.Items.Add(CreateMenuItem("Rename", "DarkMenuItem", ShowRenameOverlay));

            menu.Items.Add(CreateMenuItem("Remove", "DarkMenuItemRed",
                border => {
                    var ip = GetConsoleIP(border);
                    DeleteConsole(border);
                    RemoveConsole?.Invoke(ip);
                }));

            return menu;
        }

        public static Border CreateConsoleItem(string name, string ip)
        {
            // Hide the "no consoles" text whenever a console is created
            SetEmptyStateVisibility?.Invoke(Visibility.Collapsed);

            var consoleItem = new Border { Style = (Style)FindResource?.Invoke("ConsoleItem") };
            consoleItem.ContextMenu = CreateConsoleContextMenu(consoleItem);

            var itemGrid = new Grid();
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var controllerIcon = new Path
            {
                Data = (Geometry)FindResource?.Invoke("ControllerIcon"),
                Stroke = (Brush)FindResource?.Invoke("ColorText"),
                StrokeThickness = 1.5,
                Width = 14,
                Height = 14,
                Margin = new Thickness(16, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };
            Grid.SetColumn(controllerIcon, 0);

            var textStack = new StackPanel();
            textStack.Children.Add(new TextBlock
            {
                Text = name,
                Foreground = (Brush)FindResource?.Invoke("ColorText"),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            });
            textStack.Children.Add(new TextBlock
            {
                Text = ip,
                Foreground = (Brush)FindResource?.Invoke("ColorTextSecondary"),
                FontSize = 12
            });
            Grid.SetColumn(textStack, 1);

            var arrowIcon = new Path
            {
                Data = (Geometry)FindResource?.Invoke("ChevronRightIcon"),
                Fill = (Brush)FindResource?.Invoke("ColorText"),
                Width = 16,
                Height = 16,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0)
            };
            Grid.SetColumn(arrowIcon, 2);

            itemGrid.Children.Add(controllerIcon);
            itemGrid.Children.Add(textStack);
            itemGrid.Children.Add(arrowIcon);
            consoleItem.Child = itemGrid;

            consoleItem.MouseDown += (s, args) =>
            {
                HandleConsoleItemClick?.Invoke(consoleItem);
            };

            return consoleItem;
        }
    }
}
