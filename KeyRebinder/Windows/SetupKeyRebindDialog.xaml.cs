using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KeyRebinder.Helpers;

namespace KeyRebinder.Windows
{
    /// <summary>
    /// Interaction logic for SetupKeyRebindDialog.xaml.
    /// </summary>
    public partial class SetupKeyRebindDialog
    {
        // Using a DependencyProperty as the backing store for ShowAllProcesses.
        // This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ShowAllProcessesProperty =
            DependencyProperty.Register("ShowAllProcesses", typeof(bool), typeof(SetupKeyRebindDialog), new UIPropertyMetadata(false));

        private readonly List<RadioButton> _radioApps = new();
        private bool _forceClose;
        private RebinderInfo _results = new();
        private KeyMapping _currentKeyMapping = new();
        private static readonly BitmapImage _trashCanBitmap = new(new System.Uri("pack://application:,,,/KeyRebinder;component/Resources/trash.ico"));
        private bool _returnResults = false;
        private readonly Dictionary<string, RebinderInfo> _rebinderInfoLookup;

        public SetupKeyRebindDialog(Dictionary<string, RebinderInfo> rebinderInfoLookup)
            : this(rebinderInfoLookup, new RebinderInfo())
        {
        }

        public SetupKeyRebindDialog(Dictionary<string, RebinderInfo> rebinderInfoLookup, RebinderInfo initialRebindingInfo)
        {
            InitializeComponent();
            _rebinderInfoLookup = rebinderInfoLookup;
            _results = initialRebindingInfo;
            RefreshProcessList(this, null);
            DisplayKeyRebinders();
            if (!string.IsNullOrWhiteSpace(_results.ApplicationName))
            {
                EnableKeyRebinders();
            }
            Visibility = Visibility.Visible;
        }

        public bool ShowAllProcesses
        {
            get => (bool)GetValue(ShowAllProcessesProperty);
            set => SetValue(ShowAllProcessesProperty, value);
        }

        public RebinderInfo GetRebinderInfo()
        {
            return _returnResults ? _results : null;
        }

        public void Dispose()
        {
            _forceClose = true;

            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            if (_forceClose)
            {
                return;
            }

            e.Cancel = true;
            Hide();
        }

        private void AcceptKeyBinding(object sender, RoutedEventArgs args)
        {
            ErrorTxtBlk.Visibility = Visibility.Hidden;
            ErrorTxtBlk.Text = "";

            if (!string.IsNullOrWhiteSpace(_results.ApplicationName))
            {
                _returnResults = true;
                Close();
            }
            else
            {
                ErrorTxtBlk.Text = "Please select an application.";
                ErrorTxtBlk.Visibility = Visibility.Visible;
            }
        }

        private void CancelKeyBinding(object sender, RoutedEventArgs args)
        {
            Close();
        }

        private void RefreshProcessList(object sender, RoutedEventArgs e)
        {
            List<string> windows;

            using (AutoDisposeList<Process> processes = new(Process.GetProcesses()))
            {
                windows = new List<string>(processes
                    .Where(p => ShowAllProcesses || !string.IsNullOrEmpty(p.MainWindowTitle))
                    .Select(p => p.GetApplicationName()).Distinct());
            }

            if (_results != null && !string.IsNullOrWhiteSpace(_results.ApplicationName))
            {
                windows.Add(_results.ApplicationName);
            }

            windows = windows.Distinct().OrderBy(str => str?.ToLowerInvariant()).ToList();

            DisplayApplications(windows);
        }

        private void DisplayApplications(IList<string> applications)
        {
            Dispatcher?.Invoke(() =>
            {
                GridControl table = ProcessTable;
                table.RowDefinitions.Clear();
                table.Children.Clear();

                RowDefinition processListHeader = new() { Height = new GridLength(15) };
                table.RowDefinitions.Add(processListHeader);

                TextBlock applicationNameHeader = new()
                {
                    Text = "Application Name",
                    FontSize = 10,
                    Padding = new Thickness(15, 0, 0, 0),
                    Background = Brushes.LightGray,
                };
                Grid.SetColumn(applicationNameHeader, 1);
                Grid.SetRow(applicationNameHeader, 0);
                _ = table.Children.Add(applicationNameHeader);

                _radioApps.Clear();

                if (!string.IsNullOrWhiteSpace(_results.ApplicationName) && !applications.Contains(_results.ApplicationName))
                {
                    applications.Add(_results.ApplicationName);
                }

                for (int i = 0; i < applications.Count; i++)
                {
                    RowDefinition row = new() { Height = new GridLength(15) };
                    table.RowDefinitions.Add(row);

                    string application = applications[i];

                    RadioButton radio = new()
                    {
                        GroupName = "Apps",
                        Tag = application,
                        IsChecked = application == _results?.ApplicationName,
                    };
                    radio.Checked += (object sender, RoutedEventArgs e) =>
                    {
                        string applicationName = (string)((RadioButton)sender).Tag;
                        if (_rebinderInfoLookup.TryGetValue(applicationName, out RebinderInfo rebinderInfo))
                        {
                            _results = rebinderInfo;
                        }
                        else
                        {
                            _results.ApplicationName = applicationName;
                        }

                        EnableKeyRebinders();
                        DisplayKeyRebinders();
                    };
                    Grid.SetColumn(radio, 0);
                    Grid.SetRow(radio, 1 + i);
                    _ = table.Children.Add(radio);
                    _radioApps.Add(radio);

                    TextBlock text = new()
                    {
                        Text = application,
                        FontSize = 10,
                        Padding = new Thickness(15, 0, 0, 0),
                    };
                    Grid.SetColumn(text, 1);
                    Grid.SetRow(text, 1 + i);
                    _ = table.Children.Add(text);
                }

                table.RowDefinitions.Add(new RowDefinition());

                UpdateLayout();
            });
        }

        private void DisplayKeyRebinders()
        {
            Dispatcher?.Invoke(() =>
            {
                GridControl table = RebinderTable;
                table.RowDefinitions.Clear();
                table.Children.Clear();

                RowDefinition keyRebinderHeaderRow = new() { Height = new GridLength(15) };
                table.RowDefinitions.Add(keyRebinderHeaderRow);

                TextBlock sourceKeyHeader = new()
                {
                    Text = "Source Key",
                    FontSize = 10,
                    Padding = new Thickness(15, 0, 0, 0),
                    Background = Brushes.LightGray,
                };
                TextBlock destinationKeyHeader = new()
                {
                    Text = "Destination Key",
                    FontSize = 10,
                    Padding = new Thickness(15, 0, 0, 0),
                    Background = Brushes.LightGray,
                };

                Grid.SetColumn(sourceKeyHeader, 1);
                Grid.SetRow(sourceKeyHeader, 0);

                Grid.SetColumn(destinationKeyHeader, 2);
                Grid.SetRow(destinationKeyHeader, 0);

                _ = table.Children.Add(sourceKeyHeader);
                _ = table.Children.Add(destinationKeyHeader);

                for (int i = 0; i < _results.KeyMappings.Count; i++)
                {
                    RowDefinition row = new() { Height = new GridLength(15) };
                    table.RowDefinitions.Add(row);

                    KeyMapping keyMapping = _results.KeyMappings[i];
                    Button deleteButton = new()
                    {
                        Tag = keyMapping,
                    };
                    deleteButton.Click += (object sender, RoutedEventArgs e) =>
                    {
                        _ = _results.KeyMappings.Remove((KeyMapping)((Button)sender).Tag);
                        DisplayKeyRebinders();
                    };
                    deleteButton.Content = new Image { Source = _trashCanBitmap };

                    TextBlock sourceKeyText = new()
                    {
                        Text = keyMapping.SourceKey.ToString(),
                        FontSize = 10,
                        Padding = new Thickness(15, 0, 0, 0),
                    };
                    TextBlock destinationKeyText = new()
                    {
                        Text = keyMapping.DestinationKey.ToString(),
                        FontSize = 10,
                        Padding = new Thickness(15, 0, 0, 0),
                    };

                    Grid.SetColumn(deleteButton, 0);
                    Grid.SetRow(deleteButton, i + 1);

                    Grid.SetColumn(sourceKeyText, 1);
                    Grid.SetRow(sourceKeyText, i + 1);

                    Grid.SetColumn(destinationKeyText, 2);
                    Grid.SetRow(destinationKeyText, i + 1);

                    _ = table.Children.Add(deleteButton);
                    _ = table.Children.Add(sourceKeyText);
                    _ = table.Children.Add(destinationKeyText);
                }
            });
        }

        private void AddKeyRebinder(object sender, RoutedEventArgs e)
        {
            ErrorTxtBlk.Text = "";
            ErrorTxtBlk.Visibility = Visibility.Hidden;
            if (_currentKeyMapping.SourceKey != System.Windows.Forms.Keys.None
                && _currentKeyMapping.DestinationKey != System.Windows.Forms.Keys.None)
            {
                if (_results.KeyMappings.Contains(_currentKeyMapping))
                {
                    ErrorTxtBlk.Text += $"{_currentKeyMapping} already exists.\n";
                    ErrorTxtBlk.Visibility = Visibility.Visible;
                }
                else
                {
                    _results.KeyMappings.Add(_currentKeyMapping);

                    _currentKeyMapping = new();
                    InputKeyTextBox.Clear();
                    OutputKeyTextBox.Clear();
                    DisplayKeyRebinders();
                }
            }
            else
            {
                ErrorTxtBlk.Visibility = Visibility.Visible;
                if (_currentKeyMapping.SourceKey == System.Windows.Forms.Keys.None)
                {
                    ErrorTxtBlk.Text += "No Source Key Specified.\n";
                }
                if (_currentKeyMapping.DestinationKey == System.Windows.Forms.Keys.None)
                {
                    ErrorTxtBlk.Text += "No Destination Key Specified.\n";
                }
            }
        }

        private void InputKeyTextBox_KeyChanged(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                System.Windows.Forms.Keys key = e.ToWinforms().KeyCode;
                bool validTextBox = false;
                if (string.Equals(textBox.Name, InputKeyTextBox.Name))
                {
                    validTextBox = true;
                    _currentKeyMapping.SourceKey = key;
                }
                else if (string.Equals(textBox.Name, OutputKeyTextBox.Name))
                {
                    validTextBox = true;
                    _currentKeyMapping.DestinationKey = key;
                }
                if (validTextBox)
                {
                    textBox.Text = key.ToString();
                }
            }
        }

        private void EnableKeyRebinders()
        {
            AcceptBtn.IsEnabled = true;
            AddRebind.IsEnabled = true;
            InputKeyTextBox.IsEnabled = true;
            OutputKeyTextBox.IsEnabled = true;
        }
    }
}
