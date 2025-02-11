using ConsoleManager.Models;
using Dark.Net;
using OrbisControlAPI;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static ConsoleManager.Utilities;
using static OrbisControlAPI.OCAPI;
using System.Windows.Threading;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Linq;

namespace ConsoleManager
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly OCAPI api = new();
        public double MemoryUsageWidth { get; set; } = 300;

        // Add properties for system info
        public string FirmwareVersion { get; set; } = "?.??";
        public string ConsoleType { get; set; } = "????";
        public string CPUTemperature { get; set; } = "?? °C";
        public string SoCTemperature { get; set; } = "?? °C";
        public string PRXVersion { get; set; } = "?.??";

        public partial class App : Application { }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void ConnectToConsole(string address)
        {
            api.Connect(address);
            api.AlarmBuzzer(BuzzerModes.Single);
            api.Notify(222, "[OCAPI] Console Manager: Connected Successfully!");
            
            UpdateSystemInfo();
            UpdateProcessList();
        }

        private DispatcherTimer autoRefreshTimer;
        private DispatcherTimer signalTimer;
        private int currentSignalLevel = 0;

        public MainWindow()
        {
            InitializeComponent();

            colorSettings = ColorSettings.Load();
            darkMode = colorSettings.IsDarkMode;

            // Set up the UI operation delegates
            RemoveFromConsoleList = element => ConsoleList.Children.Remove(element);
            AddToConsoleList = element => ConsoleList.Children.Add(element);
            SetEmptyStateVisibility = visibility => EmptyState.Visibility = visibility;
            SetRenameOverlayVisibility = visibility => RenameOverlay.Visibility = visibility;
            SetRenameTextBoxText = text => RenameTextBox.Text = text;
            FocusRenameTextBox = () => RenameTextBox.Focus();
            SelectAllRenameTextBox = () => RenameTextBox.SelectAll();

            DarkNet.Instance.SetCurrentProcessTheme(darkMode ? Theme.Dark : Theme.Light);
            DarkNet.Instance.SetWindowThemeWpf(this, darkMode ? Theme.Dark : Theme.Light);

            DataContext = this;
            ColorBoxMouseDownHandler = ColorBox_MouseDown;

            // Add new delegate initializations
            Utilities.FindResource = key => FindResource(key);
            HandleConsoleItemClick = clickedItem =>
            {
                foreach (var item in ConsoleList.Children.OfType<Border>())
                    item.Background = (Brush)FindResource("ColorDarker");
                clickedItem.Background = (Brush)FindResource("ColorDarkest");
            };

            Utilities.ConnectToConsole = (ip) =>
            {
                ConnectToConsole(ip);
                UpdateSystemInfo(); // Update the UI after connection
            };
            InjectPayload = ip => api.InjectPayload(ip);
            RemoveConsole = ip => api.RemoveConsole(ip);
            DisconnectFromConsole = (ip) => 
            {
                api.Disconnect(ip);
                
                // Stop signal animation and reset bars
                signalTimer.Stop();
                currentSignalLevel = 0;
                foreach (Path bar in SignalCanvas.Children)
                {
                    bar.Opacity = 0.2;
                }
                
                // Reset system info
                UpdateSystemInfo();
            };
            AttachToConsole = (ip) => 
            {
                if (!Target.Connected)
                {
                    MessageBox.Show("Please connect to the console before attaching.", "Not Connected", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                api.Attach(ip);
                UpdateProcessList();
            };

            Utilities.UnloadPayload = (ip) => 
            {
                api.Unload(ip);
            };

            Loaded += (s, e) =>
            {
                ApplyColorSettings();

                foreach (ConsoleEntry console in api.Consoles)
                {
                    var consoleItem = CreateConsoleItem(console.CustomName ?? console.Name ?? "PS4", console.IP);
                    ConsoleList.Children.Add(consoleItem);
                }
            };

            // Initialize the timer but don't start it
            autoRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            autoRefreshTimer.Tick += (s, args) => UpdateSystemInfo();

            // Initialize signal animation timer (but don't start it)
            signalTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            signalTimer.Tick += SignalTimer_Tick;
        }

        private void ImageSource_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void ToggleDarkMode_Click(object sender, RoutedEventArgs e)
        {
            darkMode = !darkMode;
            var newTheme = darkMode ? Theme.Dark : Theme.Light;

            DarkNet.Instance.SetCurrentProcessTheme(newTheme);
            DarkNet.Instance.SetWindowThemeWpf(this, newTheme);

            var resources = Application.Current.Resources;
            var prefix = darkMode ? "Dark" : "Light";

            try
            {
                var grayColor = (Color)resources[$"{prefix}GrayColor"];
                var textColor = (Color)resources[$"{prefix}TextColor"];
                var backgroundColor = (Color)resources[$"{prefix}BackgroundColor"];
                var cardColor = (Color)resources[$"{prefix}CardColor"];
                var shadowColor = (Color)resources[$"{prefix}ShadowColor"];
                var textSecondaryColor = (Color)resources[$"{prefix}TextSecondaryColor"];

                resources["GrayColor"] = grayColor;
                resources["TextColor"] = textColor;
                resources["DarkColor"] = backgroundColor;
                resources["DarkerColor"] = cardColor;
                resources["DarkestColor"] = shadowColor;
                resources["TextSecondaryColor"] = textSecondaryColor;

                resources["ColorGray"] = new SolidColorBrush(grayColor);
                resources["ColorText"] = new SolidColorBrush(textColor);
                resources["ColorDark"] = new SolidColorBrush(backgroundColor);
                resources["ColorDarker"] = new SolidColorBrush(cardColor);
                resources["ColorDarkest"] = new SolidColorBrush(shadowColor);
                resources["ColorTextSecondary"] = new SolidColorBrush(textSecondaryColor);

                resources["CardBorderGradient"] = resources[$"{prefix}CardBorderGradient"];

                UpdateColorPreview("PrimaryColor", colorSettings.PrimaryColor);
                UpdateColorPreview("BackgroundColor", ColorToHex(backgroundColor));
                UpdateColorPreview("CardColor", ColorToHex(cardColor));
                UpdateColorPreview("ShadowColor", ColorToHex(shadowColor));
                UpdateColorPreview("GrayColor", ColorToHex(grayColor));
                UpdateColorPreview("TextColor", ColorToHex(textColor));
                UpdateColorPreview("TextSecondaryColor", ColorToHex(textSecondaryColor));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating theme: {ex.Message}");
            }
        }

        private void ColorBox_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border colorBox)
            {
                Color currentColor;

                if (colorBox.Background is LinearGradientBrush gradientBackground)
                    currentColor = gradientBackground.GradientStops[0].Color;
                else if (colorBox.Background is SolidColorBrush solidBrush)
                    currentColor = solidBrush.Color;
                else
                    currentColor = Colors.White;

                var dialog = new System.Windows.Forms.ColorDialog
                {
                    Color = System.Drawing.Color.FromArgb(
                        currentColor.R,
                        currentColor.G,
                        currentColor.B
                    ),
                    FullOpen = true
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var newColor = Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
                    var hexColor = ColorToHex(newColor);

                    var grid = colorBox.Parent as Grid;
                    if (grid != null)
                    {
                        var descriptionText = grid.Children.OfType<TextBlock>()
                            .LastOrDefault()?.Text?.ToLower() ?? "";

                        if (descriptionText.Contains("primary color"))
                        {
                            var darkerColor = Color.FromRgb(
                                (byte)(newColor.R * 0.8),
                                (byte)(newColor.G * 0.8),
                                (byte)(newColor.B * 0.8)
                            );

                            var newGradientBrush = new LinearGradientBrush(
                                newColor,
                                darkerColor,
                                new Point(0, 0),
                                new Point(1, 1)
                            );
                            colorBox.Background = newGradientBrush;
                        }
                        else
                        {
                            colorBox.Background = new SolidColorBrush(newColor);
                        }

                        var hexText = grid.Children.OfType<TextBlock>()
                            .FirstOrDefault(t => t.Text?.StartsWith("#") == true);
                        if (hexText != null)
                            hexText.Text = hexColor.ToUpper();

                        string resourceKey = "";
                        if (descriptionText.Contains("primary color"))
                            resourceKey = "PrimaryColor";
                        else if (descriptionText.Contains("background color"))
                            resourceKey = "DarkColor";
                        else if (descriptionText.Contains("cards and panels"))
                            resourceKey = "DarkerColor";
                        else if (descriptionText.Contains("darkest elements"))
                            resourceKey = "DarkestColor";
                        else if (descriptionText.Contains("borders and separators"))
                            resourceKey = "GrayColor";
                        else if (descriptionText.Contains("primary text color"))
                            resourceKey = "TextColor";
                        else if (descriptionText.Contains("less prominent text"))
                            resourceKey = "TextSecondaryColor";

                        if (!string.IsNullOrEmpty(resourceKey))
                            UpdateResourceColor(resourceKey, hexColor);
                    }
                }
            }
        }

        private void SaveChanges_Click(object sender, RoutedEventArgs e) => SaveColorSettings();

        private void ResetToDefault_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var defaultDarkColors = new ColorSettings
                {
                    IsDarkMode = darkMode,
                    PrimaryColor = ColorToHex((Color)Application.Current.Resources["PrimaryColor"]),
                    GrayColor = ColorToHex((Color)Application.Current.Resources["DarkGrayColor"]),
                    TextColor = ColorToHex((Color)Application.Current.Resources["DarkTextColor"]),
                    BackgroundColor = ColorToHex((Color)Application.Current.Resources["DarkBackgroundColor"]),
                    CardColor = ColorToHex((Color)Application.Current.Resources["DarkCardColor"]),
                    ShadowColor = ColorToHex((Color)Application.Current.Resources["DarkShadowColor"]),
                    TextSecondaryColor = ColorToHex((Color)Application.Current.Resources["DarkTextSecondaryColor"])
                };

                colorSettings.IsDarkMode = darkMode;
                colorSettings.PrimaryColor = defaultDarkColors.PrimaryColor;
                colorSettings.GrayColor = defaultDarkColors.GrayColor;
                colorSettings.TextColor = defaultDarkColors.TextColor;
                colorSettings.BackgroundColor = defaultDarkColors.BackgroundColor;
                colorSettings.CardColor = defaultDarkColors.CardColor;
                colorSettings.ShadowColor = defaultDarkColors.ShadowColor;
                colorSettings.TextSecondaryColor = defaultDarkColors.TextSecondaryColor;

                ApplyColorSettings();

                UpdateColorPreview("PrimaryColor", defaultDarkColors.PrimaryColor);
                UpdateColorPreview("BackgroundColor", defaultDarkColors.BackgroundColor);
                UpdateColorPreview("CardColor", defaultDarkColors.CardColor);
                UpdateColorPreview("ShadowColor", defaultDarkColors.ShadowColor);
                UpdateColorPreview("GrayColor", defaultDarkColors.GrayColor);
                UpdateColorPreview("TextColor", defaultDarkColors.TextColor);
                UpdateColorPreview("TextSecondaryColor", defaultDarkColors.TextSecondaryColor);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error resetting colors: {ex.Message}", "Reset Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowAddConsoleOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (AddConsoleOverlay == null) return;
            AddConsoleOverlay.Visibility = Visibility.Visible;

            // Clear previous results
            FoundConsolesPanel.Children.Clear();

            // Start real console discovery and add them to UI
            api.FindConsoles(consoles =>
            {
                Dispatcher.Invoke(() => 
                {
                    foreach (var console in consoles)
                    {
                        AddConsoleToPanel(console);
                        Debug.WriteLine($"IP: {console.IP}, Name: {console.SystemName}, " +
                                      $"Firmware: {console.Firmware}, OrbisControl: {console.OrbisControl}, " +
                                      $"Type: {console.ConsoleType}");
                    }
                });
            });
        }

        private void AddConsoleToPanel(FoundConsole console)
        {
            var consoleBox = new Border
            {
                Style = (Style)FindResource("CardBorder"),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(16)
            };

            var content = new StackPanel();
            
            content.Children.Add(new TextBlock
            {
                Text = console.SystemName ?? "PS4",
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Foreground = (Brush)FindResource("ColorText"),
                Margin = new Thickness(0, 0, 0, 4)
            });

            content.Children.Add(new TextBlock
            {
                Text = $"IP: {console.IP}",
                Foreground = (Brush)FindResource("ColorTextSecondary"),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var infoPanel = new StackPanel { Orientation = Orientation.Horizontal };
            infoPanel.Children.Add(new TextBlock
            {
                Text = $"Firmware {console.Firmware}",
                Foreground = (Brush)FindResource("ColorTextSecondary"),
                FontSize = 12
            });

            infoPanel.Children.Add(new TextBlock
            {
                Text = $" • OrbisControl {console.OrbisControl}",
                Foreground = (Brush)FindResource("ColorTextSecondary"),
                FontSize = 12,
                Margin = new Thickness(8, 0, 0, 0)
            });

            content.Children.Add(infoPanel);
            consoleBox.Child = content;

            // Update click handler to fill both name and IP
            consoleBox.MouseDown += (s, args) =>
            {
                var nameBox = AddConsoleOverlay.FindVisualChildren<TextBox>()
                    .FirstOrDefault();  // First TextBox is the name box
                var ipBox = AddConsoleOverlay.FindVisualChildren<TextBox>()
                    .Skip(1).FirstOrDefault();  // Second TextBox is the IP box

                if (nameBox != null) nameBox.Text = console.SystemName ?? "PS4";
                if (ipBox != null) ipBox.Text = console.IP;
            };

            FoundConsolesPanel.Children.Add(consoleBox);
        }

        private void CloseAddConsoleOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (AddConsoleOverlay != null)
                AddConsoleOverlay.Visibility = Visibility.Collapsed;
        }

        private void AddConsole_Click(object sender, RoutedEventArgs e)
        {
            var ipAddressBox = this.FindVisualChildren<TextBox>()
                .FirstOrDefault(tb => tb.Margin.Bottom == 0);
            var nameBox = this.FindVisualChildren<TextBox>()
                .FirstOrDefault(tb => tb.Margin.Bottom == 16);

            if (ipAddressBox == null || string.IsNullOrWhiteSpace(ipAddressBox.Text))
            {
                MessageBox.Show("Please enter an IP address.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string ip = ipAddressBox.Text;
            string name = string.IsNullOrWhiteSpace(nameBox?.Text) ? "PS4" : nameBox.Text;

            api.AddConsole(ip, name);
            var consoleItem = CreateConsoleItem(name, ip);

            if (EmptyState.Visibility == Visibility.Visible)
                EmptyState.Visibility = Visibility.Collapsed;

            ConsoleList.Children.Add(consoleItem);

            if (ipAddressBox != null) ipAddressBox.Text = "";
            if (nameBox != null) nameBox.Text = "";

            CloseAddConsoleOverlay_Click(sender, e);
        }

        private void OverlayBackground_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.Source is Border border && border.Background is SolidColorBrush brush && brush.Color.A == 128)
            {
                if (AddConsoleOverlay.Visibility == Visibility.Visible)
                    CloseAddConsoleOverlay_Click(sender, new RoutedEventArgs());
                else if (RenameOverlay.Visibility == Visibility.Visible)
                    CloseRenameOverlay_Click(sender, new RoutedEventArgs());
            }
        }

        private void CloseRenameOverlay_Click(object sender, RoutedEventArgs e)
        {
            RenameOverlay.Visibility = Visibility.Collapsed;
            currentConsoleToRename = null;
        }

        private void ConfirmRename_Click(object sender, RoutedEventArgs e)
        {
            if (currentConsoleToRename != null && !string.IsNullOrWhiteSpace(RenameTextBox.Text))
            {
                var grid = currentConsoleToRename.Child as Grid;
                var textStack = grid?.Children.OfType<StackPanel>().FirstOrDefault();
                var nameTextBlock = textStack?.Children.OfType<TextBlock>().FirstOrDefault();

                if (nameTextBlock != null)
                    nameTextBlock.Text = RenameTextBox.Text;
            }

            CloseRenameOverlay_Click(sender, e);
        }

        // Method to update system info
        private void UpdateSystemInfo()
        {
            if (api.Connected)
            {
                api.GetTargetInfo();
                FirmwareVersion = Target.Firmware.ToString("0.00");
                ConsoleType = Target.ConsoleType ?? "????";
                CPUTemperature = $"{Target.CPUTemp} °C";
                SoCTemperature = $"{Target.SoCTemp} °C";
                PRXVersion = Target.Version.ToString("0.00");

                // Start signal animation if not already running
                if (!signalTimer.IsEnabled)
                {
                    signalTimer.Start();
                }
            }
            else
            {
                // Reset all values and stop animation
                FirmwareVersion = "?.??";
                ConsoleType = "????";
                CPUTemperature = "?? °C";
                SoCTemperature = "?? °C";
                PRXVersion = "?.??";

                signalTimer.Stop();
                currentSignalLevel = 0;
                foreach (Path bar in SignalCanvas.Children)
                {
                    bar.Opacity = 0.2;
                }
            }

            OnPropertyChanged(nameof(FirmwareVersion));
            OnPropertyChanged(nameof(ConsoleType));
            OnPropertyChanged(nameof(CPUTemperature));
            OnPropertyChanged(nameof(SoCTemperature));
            OnPropertyChanged(nameof(PRXVersion));
        }

        private void RefreshSystemInfo_Click(object sender, RoutedEventArgs e)
        {
            UpdateSystemInfo();
        }

        private void SignalTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                currentSignalLevel = (currentSignalLevel % 5) + 1;
                
                foreach (Path bar in SignalCanvas.Children)
                    bar.Opacity = 0.2;

                for (int i = 0; i < currentSignalLevel; i++)
                {
                    if (SignalCanvas.Children[i] is Path bar)
                        bar.Opacity = 1.0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Signal animation error: {ex.Message}");
            }
        }

        private void UpdateProcessList()
        {
            if (api.Connected)
            {
                ProcessListComboBox.Items.Clear();
                string[] processes = ProcessInfo.List;
                
                if (processes != null)
                {
                    foreach (string process in processes.OrderBy(p => p))
                    {
                        ProcessListComboBox.Items.Add(new ComboBoxItem 
                        { 
                            Content = process,
                            Style = (Style)FindResource("DarkComboBoxItem")
                        });
                    }
                }
            }
        }

        private void ProcessList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProcessListComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string selectedProcess = selectedItem.Content.ToString();
            }
        }
    }
}