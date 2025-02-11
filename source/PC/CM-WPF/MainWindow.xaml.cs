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

namespace ConsoleManager
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly OCAPI api = new();
        public double MemoryUsageWidth { get; set; } = 300;

        public string FirmwareVersion { get; set; } = "----";
        public string ConsoleType { get; set; } = "----";
        public string CPUTemperature { get; set; } = "---";
        public string SoCTemperature { get; set; } = "---";
        public string PRXVersion { get; set; } = "----";
        public string DLLVersion { get; set; } = "----";

        public double DiskUsagePercentage { get; set; } = 0;
        public double DiskUsageWidth { get; set; } = 0;
        public string TotalDiskSpace { get; set; } = "----";
        public string FreeDiskSpace { get; set; } = "----";
        public string UsedDiskSpace { get; set; } = "----";

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
            api.SendNotification("[OCAPI] Console Manager: Connected Successfully!");

            UpdateSystemInfo();
            UpdateProcessList();
            UpdateDiskInfo();
        }

        private DispatcherTimer autoRefreshTimer;
        private DispatcherTimer signalTimer;
        private int currentSignalLevel = 0;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            colorSettings = ColorSettings.Load();
            darkMode = colorSettings.IsDarkMode;

            RemoveFromConsoleList = element => ConsoleList.Children.Remove(element);
            AddToConsoleList = element => ConsoleList.Children.Add(element);
            SetEmptyStateVisibility = visibility => EmptyState.Visibility = visibility;
            SetRenameOverlayVisibility = visibility => RenameOverlay.Visibility = visibility;
            SetRenameTextBoxText = text => RenameTextBox.Text = text;
            FocusRenameTextBox = () => RenameTextBox.Focus();
            SelectAllRenameTextBox = () => RenameTextBox.SelectAll();

            DarkNet.Instance.SetCurrentProcessTheme(darkMode ? Theme.Dark : Theme.Light);
            DarkNet.Instance.SetWindowThemeWpf(this, darkMode ? Theme.Dark : Theme.Light);

            ColorBoxMouseDownHandler = ColorBox_MouseDown;

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
                UpdateSystemInfo();
            };
            InjectPayload = ip => api.InjectPayload(ip);
            RemoveConsole = ip => api.RemoveConsole(ip);
            Utilities.DisconnectFromConsole = ip => DisconnectFromConsole(ip);
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

            Utilities.UnloadPayload = ip => UnloadPayload(ip);

            Loaded += (s, e) =>
            {
                ApplyColorSettings();

                foreach (ConsoleEntry console in api.Consoles)
                {
                    var consoleItem = CreateConsoleItem(console.CustomName ?? console.Name ?? "PS4", console.IP);
                    ConsoleList.Children.Add(consoleItem);
                }

                EmptyState.Visibility = ConsoleList.Children.Count <= 1 ? 
                    Visibility.Visible : Visibility.Collapsed;
            };

            autoRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            autoRefreshTimer.Tick += (s, args) => UpdateSystemInfo();

            signalTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            signalTimer.Tick += SignalTimer_Tick;

            DLLVersion = api.Version;
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

            FoundConsolesPanel.Children.Clear();

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

            consoleBox.MouseDown += (s, args) =>
            {
                var nameBox = AddConsoleOverlay.FindVisualChildren<TextBox>()
                    .FirstOrDefault();
                var ipBox = AddConsoleOverlay.FindVisualChildren<TextBox>()
                    .Skip(1).FirstOrDefault();

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

        private Border CreateConsoleItem(string name, string ip)
        {
            var consoleBox = new Border
            {
                Style = (Style)FindResource("CardBorder"),
                Margin = new Thickness(8, 4, 8, 4),
                Padding = new Thickness(16)
            };

            var grid = new Grid();
            
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textStack = new StackPanel();

            var nameBlock = new TextBlock
            {
                Text = name,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Foreground = (Brush)FindResource("ColorText")
            };

            var ipBlock = new TextBlock
            {
                Text = $"IP: {ip}",
                Foreground = (Brush)FindResource("ColorTextSecondary"),
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 0)
            };

            textStack.Children.Add(nameBlock);
            textStack.Children.Add(ipBlock);
            Grid.SetColumn(textStack, 0);
            grid.Children.Add(textStack);

            var arrowPath = new Path
            {
                Data = (Geometry)FindResource("ChevronRightIcon"),
                Fill = (Brush)FindResource("ColorTextSecondary"),
                Width = 20,
                Height = 20,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(arrowPath, 1);
            grid.Children.Add(arrowPath);

            consoleBox.Child = grid;

            var contextMenu = new ContextMenu { Style = (Style)FindResource("DarkContextMenu") };
            
            var injectItem = new MenuItem 
            { 
                Header = "Inject",
                Style = (Style)FindResource("DarkMenuItem")
            };
            injectItem.Click += (s, e) => Utilities.InjectPayload(ip);
            
            var unloadItem = new MenuItem 
            { 
                Header = "Unload",
                Style = (Style)FindResource("DarkMenuItem")
            };
            unloadItem.Click += (s, e) => Utilities.UnloadPayload(ip);
            
            var connectItem = new MenuItem 
            { 
                Header = "Connect",
                Style = (Style)FindResource("DarkMenuItem")
            };
            connectItem.Click += (s, e) => Utilities.ConnectToConsole(ip);
            
            var attachItem = new MenuItem 
            { 
                Header = "Attach",
                Style = (Style)FindResource("DarkMenuItem")
            };
            attachItem.Click += (s, e) => Utilities.AttachToConsole(ip);
            
            var disconnectItem = new MenuItem 
            { 
                Header = "Disconnect",
                Style = (Style)FindResource("DarkMenuItem")
            };
            disconnectItem.Click += (s, e) => Utilities.DisconnectFromConsole(ip);
            
            var renameItem = new MenuItem 
            { 
                Header = "Rename",
                Style = (Style)FindResource("DarkMenuItem")
            };
            renameItem.Click += (s, e) =>
            {
                RenameOverlay.Visibility = Visibility.Visible;
                RenameTextBox.Text = name;
                RenameTextBox.Focus();
                RenameTextBox.SelectAll();
                currentConsoleToRename = consoleBox;
            };
            
            var removeItem = new MenuItem 
            { 
                Header = "Remove",
                Style = (Style)FindResource("DarkMenuItemRed")
            };
            removeItem.Click += (s, e) => Utilities.RemoveConsole(ip);

            contextMenu.Items.Add(injectItem);
            contextMenu.Items.Add(unloadItem);
            contextMenu.Items.Add(new Separator { Style = (Style)FindResource("MenuSeparator") });
            
            contextMenu.Items.Add(connectItem);
            contextMenu.Items.Add(attachItem);
            contextMenu.Items.Add(disconnectItem);
            contextMenu.Items.Add(new Separator { Style = (Style)FindResource("MenuSeparator") });
            
            contextMenu.Items.Add(renameItem);
            contextMenu.Items.Add(removeItem);

            consoleBox.ContextMenu = contextMenu;

            return consoleBox;
        }

        private void AddConsole_Click(object sender, RoutedEventArgs e)
        {
            var ipAddressBox = this.FindVisualChildren<TextBox>()
                .FirstOrDefault(tb => tb.Margin.Bottom == 0);
            var nameBox = ConsoleNameTextBox;

            if (ipAddressBox == null || string.IsNullOrWhiteSpace(ipAddressBox.Text))
            {
                MessageBox.Show("Please enter an IP address.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string ip = ipAddressBox.Text.Trim();
            string name = string.IsNullOrWhiteSpace(nameBox?.Text) ? "PS4" : nameBox.Text.Trim();

            bool isDuplicate = ConsoleList.Children.OfType<Border>().Any(border =>
            {
                var stack = border.Child as Grid;
                var textStack = stack?.Children[0] as StackPanel;
                var ipText = (textStack?.Children[1] as TextBlock)?.Text;
                var existingIp = ipText?.Replace("IP: ", "").Trim();
                return existingIp == ip;
            });

            if (isDuplicate)
            {
                var existingConsole = ConsoleList.Children.OfType<Border>().First(border =>
                {
                    var stack = border.Child as Grid;
                    var textStack = stack?.Children[0] as StackPanel;
                    var ipText = (textStack?.Children[1] as TextBlock)?.Text;
                    return ipText?.Replace("IP: ", "").Trim() == ip;
                });

                var stack = existingConsole.Child as Grid;
                var textStack = stack?.Children[0] as StackPanel;
                var nameBlock = textStack?.Children[0] as TextBlock;
                if (nameBlock != null)
                {
                    nameBlock.Text = name;
                    api.RenameConsole(ip, name);
                }
            }
            else
            {
                api.AddConsole(ip, name);
                var consoleItem = CreateConsoleItem(name, ip);
                ConsoleList.Children.Add(consoleItem);
            }

            EmptyState.Visibility = ConsoleList.Children.Count <= 1 ? 
                Visibility.Visible : Visibility.Collapsed;

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
                var textStack = grid?.Children[0] as StackPanel;
                var ipBlock = textStack?.Children[1] as TextBlock;
                
                // Extract IP from the text block (removes "IP: " prefix)
                string ip = ipBlock?.Text.Replace("IP: ", "").Trim();
                
                if (!string.IsNullOrEmpty(ip))
                {
                    // Update the name in the UI
                    var nameBlock = textStack?.Children[0] as TextBlock;
                    if (nameBlock != null)
                        nameBlock.Text = RenameTextBox.Text;

                    // Update the name in the API
                    api.RenameConsole(ip, RenameTextBox.Text);
                }
            }

            CloseRenameOverlay_Click(sender, e);
        }

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
            OnPropertyChanged(nameof(DLLVersion));
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

        private void UpdateDiskInfo()
        {
            if (api?.Connected == true)
            {
                var diskInfo = OCAPI.TargetInfo.Storage;

                string percentageStr = diskInfo.PercentageUsed.Replace("%", "").Trim();
                if (double.TryParse(percentageStr, out double percentage))
                {
                    DiskUsagePercentage = percentage;
                }
                else
                {
                    DiskUsagePercentage = 0;
                }

                TotalDiskSpace = diskInfo.Total;
                FreeDiskSpace = diskInfo.Free;
                UsedDiskSpace = diskInfo.Used;

                OnPropertyChanged(nameof(DiskUsagePercentage));
                OnPropertyChanged(nameof(TotalDiskSpace));
                OnPropertyChanged(nameof(FreeDiskSpace));
                OnPropertyChanged(nameof(UsedDiskSpace));
            }
            else
            {
                DiskUsagePercentage = 0;
                TotalDiskSpace = "?.?? GB";
                FreeDiskSpace = "?.?? GB";
                UsedDiskSpace = "?.?? GB";
            }
        }

        private void ResetHomePageData()
        {
            // Reset system info
            FirmwareVersion = "?.??";
            ConsoleType = "????";
            CPUTemperature = "?? °C";
            SoCTemperature = "?? °C";
            PRXVersion = "?.??";

            // Reset disk info
            DiskUsagePercentage = 0;
            TotalDiskSpace = "?.?? GB";
            FreeDiskSpace = "?.?? GB";
            UsedDiskSpace = "?.?? GB";

            // Clear process list
            ProcessListComboBox.Items.Clear();

            // Stop signal animation and reset bars
            signalTimer.Stop();
            currentSignalLevel = 0;
            foreach (Path bar in SignalCanvas.Children)
            {
                bar.Opacity = 0.2;
            }

            // Notify UI of property changes
            OnPropertyChanged(nameof(FirmwareVersion));
            OnPropertyChanged(nameof(ConsoleType));
            OnPropertyChanged(nameof(CPUTemperature));
            OnPropertyChanged(nameof(SoCTemperature));
            OnPropertyChanged(nameof(PRXVersion));
            OnPropertyChanged(nameof(DiskUsagePercentage));
            OnPropertyChanged(nameof(TotalDiskSpace));
            OnPropertyChanged(nameof(FreeDiskSpace));
            OnPropertyChanged(nameof(UsedDiskSpace));
        }

        private void DisconnectFromConsole(string ip)
        {
            api.Disconnect(ip);
            ResetHomePageData();
        }

        private void UnloadPayload(string ip)
        {
            api.Unload(ip);
            ResetHomePageData();
        }
    }
}