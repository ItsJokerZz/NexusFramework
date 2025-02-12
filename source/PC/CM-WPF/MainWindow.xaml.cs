using ConsoleManager.Models;
using Dark.Net;
using OrbisControlAPI;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using static ConsoleManager.Utilities;
using static OrbisControlAPI.OCAPI;

namespace ConsoleManager
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Common Properties and Fields
        private readonly OCAPI api = new();
        private DispatcherTimer autoRefreshTimer;
        private DispatcherTimer signalTimer;
        private int currentSignalLevel = 0;
        private Action<string> RemoveConsole;
        private Border currentConsoleToRename;
        private bool darkMode;
        private ColorSettings colorSettings;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public partial class App : Application { }
        #endregion

        #region Home Page Properties
        public string FirmwareVersion { get; set; } = "----";
        public string ConsoleType { get; set; } = "----";
        public string CPUTemperature { get; set; } = "---";
        public string SoCTemperature { get; set; } = "---";
        public string PRXVersion { get; set; } = "----";
        public string DLLVersion { get; set; } = "----";
        #endregion

        #region System Page Properties
        public double DiskUsagePercentage { get; set; } = 0;
        public double DiskUsageWidth { get; set; } = 0;
        public string TotalDiskSpace { get; set; } = "----";
        public string FreeDiskSpace { get; set; } = "----";
        public string UsedDiskSpace { get; set; } = "----";
        #endregion

        #region Memory Page Properties
        public double MemoryUsageWidth { get; set; } = 300;
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            colorSettings = ColorSettings.Load();
            darkMode = ColorSettings.IsDarkMode;

            RemoveConsole = ip =>
            {
                // Remove from API
                api.RemoveConsole(ip);

                // Find and remove the console's card from UI
                var consoleToRemove = ConsoleList.Children.OfType<Border>()
                    .FirstOrDefault(border =>
                    {
                        var grid = border.Child as Grid;
                        var textStack = grid?.Children[0] as StackPanel;
                        var ipText = (textStack?.Children[1] as TextBlock)?.Text;
                        return ipText?.Replace("IP: ", "").Trim() == ip;
                    });

                if (consoleToRemove != null)
                {
                    ConsoleList.Children.Remove(consoleToRemove);

                    // Update empty state visibility
                    EmptyState.Visibility = ConsoleList.Children.Count <= 1 ?
                        Visibility.Visible : Visibility.Collapsed;
                }
            };
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

            // Populate system images from NotificationImages class
            Type type = typeof(NotificationImages);
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            SystemImagesComboBox.Items.Clear();
            foreach (FieldInfo field in fields)
            {
                SystemImagesComboBox.Items.Add(new ComboBoxItem
                {
                    Content = field.Name,
                    Style = (Style)FindResource("DarkComboBoxItem")
                });
            }

            // Ensure System Image is selected and visible
            NotificationTypeComboBox.SelectedIndex = 0;  // Select "System Image"
            SystemImagesComboBox.SelectedIndex = 0;  // Select first system image
            SystemImagesComboBox.Visibility = Visibility.Visible;
            CustomImageUrlTextBox.Visibility = Visibility.Collapsed;
        }

        #region Console Management Methods
        private void ConnectToConsole(string address)
        {
            api.Connect(address);
            api.AlarmBuzzer(BuzzerModes.Single);
            api.SendNotification("[OCAPI] Console Manager: Connected Successfully!");

            UpdateSystemInfo();
            UpdateProcessList();
            UpdateDiskInfo();

            int id = api.GetProcessIdByName("SceShellUI");
            string name = api.GetNameOfProcessByID(id);
        }

        private void DisconnectFromConsole(string ip)
        {
            api.AlarmBuzzer(BuzzerModes.Double);
            api.Disconnect(ip);
            ResetHomePageData();
        }

        private void UnloadPayload(string ip)
        {
            api.Unload(ip);
            ResetHomePageData();
        }

        private Border CreateConsoleItem(string name, string ip)
        {
            var console = api.Consoles.FirstOrDefault(c => c.IP == ip);
            string displayName = console?.CustomName;

            if (string.IsNullOrEmpty(displayName))
                displayName = console?.Name ?? name ?? "PS4";

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
                Text = displayName,
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

            removeItem.Click += (s, e) => RemoveConsole(ip);

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

        private void AddConsoleToPanel(FoundConsole console)
        {
            var existingConsole = api.Consoles.FirstOrDefault(c => c.IP == console.IP);
            string displayName = existingConsole?.CustomName;
            if (string.IsNullOrEmpty(displayName))
                displayName = console.SystemName ?? "PS4";

            var consoleBox = new Border
            {
                Style = (Style)FindResource("CardBorder"),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(16)
            };

            var content = new StackPanel();

            content.Children.Add(new TextBlock
            {
                Text = displayName,
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
                        AddConsoleToPanel(console);

                });
            });
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

                string ip = ipBlock?.Text.Replace("IP: ", "").Trim();

                if (!string.IsNullOrEmpty(ip))
                {
                    var nameBlock = textStack?.Children[0] as TextBlock;
                    if (nameBlock != null)
                        nameBlock.Text = RenameTextBox.Text;

                    api.RenameConsole(ip, RenameTextBox.Text);
                }
            }

            CloseRenameOverlay_Click(sender, e);
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
        #endregion

        #region Home Page Methods
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

                if (!signalTimer.IsEnabled)
                {
                    signalTimer.Start();
                }
            }
            else
            {
                ResetHomePageData();
            }

            OnPropertyChanged(nameof(FirmwareVersion));
            OnPropertyChanged(nameof(ConsoleType));
            OnPropertyChanged(nameof(CPUTemperature));
            OnPropertyChanged(nameof(SoCTemperature));
            OnPropertyChanged(nameof(PRXVersion));
            OnPropertyChanged(nameof(DLLVersion));
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

        private void ResetHomePageData()
        {
            FirmwareVersion = "----";
            ConsoleType = "----";
            CPUTemperature = "---";
            SoCTemperature = "---";
            PRXVersion = "----";

            DiskUsagePercentage = 0;
            TotalDiskSpace = "----";
            FreeDiskSpace = "----";
            UsedDiskSpace = "----";

            ProcessListComboBox.Items.Clear();

            signalTimer.Stop();
            currentSignalLevel = 0;
            foreach (Path bar in SignalCanvas.Children)
            {
                bar.Opacity = 0.2;
            }

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
        #endregion

        #region System Page Methods
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

        private void SetFanThreshold_Click(object sender, RoutedEventArgs e)
            => api.SetFanThreshold((int)FanThresholdSlider.Value);
        private void NotificationType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SystemImagesComboBox == null || CustomImageUrlTextBox == null || NotificationTypeComboBox?.SelectedItem == null)
                return;

            if (NotificationTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                switch (selectedItem.Content.ToString())
                {
                    case "System Image":
                        SystemImagesComboBox.Visibility = Visibility.Visible;
                        CustomImageUrlTextBox.Visibility = Visibility.Collapsed;
                        break;
                    case "Custom Image":
                        SystemImagesComboBox.Visibility = Visibility.Collapsed;
                        CustomImageUrlTextBox.Visibility = Visibility.Visible;
                        break;
                    default:
                        SystemImagesComboBox.Visibility = Visibility.Collapsed;
                        CustomImageUrlTextBox.Visibility = Visibility.Collapsed;
                        break;
                }
            }
        }

        private void SendNotification_Click(object sender, RoutedEventArgs e)
        {
            string message = NotificationTextBox.Text;
            if (string.IsNullOrWhiteSpace(message))
            {
                MessageBox.Show("Please enter a notification message.", "Empty Message", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (NotificationTypeComboBox.SelectedItem is ComboBoxItem selectedType)
            {
                if (selectedType.Content.ToString() == "System Image"
                    && SystemImagesComboBox.SelectedItem is ComboBoxItem selectedImage)
                {
                    string imageName = selectedImage.Content.ToString();
                    Type type = typeof(NotificationImages);
                    FieldInfo field = type.GetField(imageName);
                    if (field != null)
                    {
                        string imageUrl = (string)field.GetValue(null);
                        api.SendNotification(message, imageUrl);
                    }
                }
                else if (selectedType.Content.ToString() == "Custom Image")
                {
                    string imageUrl = CustomImageUrlTextBox.Text;
                    if (!string.IsNullOrWhiteSpace(imageUrl))
                        api.SendNotification(message, imageUrl);
                    else
                        api.SendNotification(message);
                }
                else
                    api.SendNotification(message);
            }

            NotificationTextBox.Clear();
        }

        private void ActivateBuzzer_Click(object sender, RoutedEventArgs e)
        {
            BuzzerModes mode = BuzzerModes.Single;

            var stackPanel = this.FindVisualChildren<StackPanel>()
                .FirstOrDefault(sp => sp.Children.OfType<RadioButton>()
                    .Any(rb => rb.Content?.ToString() == "Single"));

            if (stackPanel != null)
            {
                var radioButtons = stackPanel.Children.OfType<RadioButton>();
                foreach (var rb in radioButtons)
                {
                    if (rb.IsChecked == true)
                    {
                        mode = rb.Content.ToString() switch
                        {
                            "Single" => BuzzerModes.Single,
                            "Double" => BuzzerModes.Double,
                            "Triple" => BuzzerModes.Triple,
                            "Continuous" => BuzzerModes.Continuous,
                            _ => BuzzerModes.Single
                        };
                        break;
                    }
                }
            }

            api.AlarmBuzzer(mode);
        }

        private void BuzzerMode_Changed(object sender, RoutedEventArgs e)
        {
            if (Target.Connected) api.AlarmBuzzer(BuzzerModes.Stop);
        }
        #endregion

        #region Settings Page Methods
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
                        else if (descriptionText.Contains("main background"))
                            resourceKey = "DarkColor";
                        else if (descriptionText.Contains("cards and panels"))
                            resourceKey = "DarkerColor";
                        else if (descriptionText.Contains("darkest elements"))
                            resourceKey = "DarkestColor";
                        else if (descriptionText.Contains("borders and separators"))
                            resourceKey = "GrayColor";
                        else if (descriptionText.Contains("primary text"))
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

            // Keep the current dark mode state
            bool currentDarkMode = ColorSettings.IsDarkMode;

            // Get default colors based on current theme
            var defaultColors = new ColorSettings
            {
                PrimaryColor = "#0072CE",      // Primary color is same for both themes
                PrimaryColorDark = "#004B87",  // Primary dark color is same for both themes
                GrayColor = ColorToHex((Color)Application.Current.Resources[currentDarkMode ? "DarkGrayColor" : "LightGrayColor"]),
                TextColor = ColorToHex((Color)Application.Current.Resources[currentDarkMode ? "DarkTextColor" : "LightTextColor"]),
                BackgroundColor = ColorToHex((Color)Application.Current.Resources[currentDarkMode ? "DarkBackgroundColor" : "LightBackgroundColor"]),
                CardColor = ColorToHex((Color)Application.Current.Resources[currentDarkMode ? "DarkCardColor" : "LightCardColor"]),
                ShadowColor = ColorToHex((Color)Application.Current.Resources[currentDarkMode ? "DarkShadowColor" : "LightShadowColor"]),
                TextSecondaryColor = ColorToHex((Color)Application.Current.Resources[currentDarkMode ? "DarkTextSecondaryColor" : "LightTextSecondaryColor"])
            };

            ColorSettings.IsDarkMode = currentDarkMode;

            var newTheme = currentDarkMode ? Theme.Dark : Theme.Light;
            DarkNet.Instance.SetCurrentProcessTheme(newTheme);
            DarkNet.Instance.SetWindowThemeWpf(this, newTheme);

            colorSettings = defaultColors;

            var primaryColor = (Color)ColorConverter.ConvertFromString(defaultColors.PrimaryColor);
            var primaryDarkColor = (Color)ColorConverter.ConvertFromString(defaultColors.PrimaryColorDark);

            Application.Current.Resources["PrimaryColor"] = primaryColor;
            Application.Current.Resources["PrimaryColorDark"] = primaryDarkColor;

            var newGradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };

            newGradient.GradientStops.Add(new GradientStop(primaryColor, 0));
            newGradient.GradientStops.Add(new GradientStop(primaryDarkColor, 1));

            Application.Current.Resources["PrimaryGradient"] = newGradient;
            Application.Current.Resources["PrimaryBrush"] = newGradient.Clone();
            Application.Current.Resources["ColorPrimary"] = newGradient.Clone();

            Application.Current.Resources["GrayColor"] = (Color)ColorConverter.ConvertFromString(defaultColors.GrayColor);
            Application.Current.Resources["TextColor"] = (Color)ColorConverter.ConvertFromString(defaultColors.TextColor);
            Application.Current.Resources["DarkColor"] = (Color)ColorConverter.ConvertFromString(defaultColors.BackgroundColor);
            Application.Current.Resources["DarkerColor"] = (Color)ColorConverter.ConvertFromString(defaultColors.CardColor);
            Application.Current.Resources["DarkestColor"] = (Color)ColorConverter.ConvertFromString(defaultColors.ShadowColor);
            Application.Current.Resources["TextSecondaryColor"] = (Color)ColorConverter.ConvertFromString(defaultColors.TextSecondaryColor);

            Application.Current.Resources["ColorGray"] = new SolidColorBrush((Color)Application.Current.Resources["GrayColor"]);
            Application.Current.Resources["ColorText"] = new SolidColorBrush((Color)Application.Current.Resources["TextColor"]);
            Application.Current.Resources["ColorDark"] = new SolidColorBrush((Color)Application.Current.Resources["DarkColor"]);
            Application.Current.Resources["ColorDarker"] = new SolidColorBrush((Color)Application.Current.Resources["DarkerColor"]);
            Application.Current.Resources["ColorDarkest"] = new SolidColorBrush((Color)Application.Current.Resources["DarkestColor"]);
            Application.Current.Resources["ColorTextSecondary"] = new SolidColorBrush((Color)Application.Current.Resources["TextSecondaryColor"]);

            var grid = this.FindVisualChildren<Grid>()
                .FirstOrDefault(g => g.Children.OfType<TextBlock>()
                    .Any(tb => tb.Text?.Contains("Primary color used for buttons") == true));

            if (grid != null)
            {
                var colorBox = grid.Children.OfType<Border>().FirstOrDefault();
                if (colorBox != null)
                    colorBox.Background = newGradient;
            }

            UpdateColorPreview("BackgroundColor", defaultColors.BackgroundColor);
            UpdateColorPreview("CardColor", defaultColors.CardColor);
            UpdateColorPreview("ShadowColor", defaultColors.ShadowColor);
            UpdateColorPreview("GrayColor", defaultColors.GrayColor);
            UpdateColorPreview("TextColor", defaultColors.TextColor);
            UpdateColorPreview("TextSecondaryColor", defaultColors.TextSecondaryColor);
        }
        #endregion

    }
}