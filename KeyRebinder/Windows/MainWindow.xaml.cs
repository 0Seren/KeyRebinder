using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Linq;
using KeyRebinder.Helpers;
using WindowsInput.Native;
using WindowsInput;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

namespace KeyRebinder.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml.
    /// </summary>
    public partial class MainWindow
    {
        private readonly Dictionary<string, RebinderInfo> _rebinderInfoLookup = new();
        private bool _forceClose;
        private bool _isHiding;
        private string _activeProcessName = null;
        private readonly GlobalKeyboardHook _globalKeyboardHook;
        private readonly InputSimulator _inputSimulator = new();
        private static readonly BitmapImage _trashCanBitmap = new(new Uri("pack://application:,,,/KeyRebinder;component/Resources/trash.ico"));
        private const string _savedRebindingsFileLocation = "rebindings.txt";

        public MainWindow()
        {
            InitializeComponent();
            LoadRebinderInfo();
            InitializeProcessMonitor();
            StateChanged += Window_StateChanged;

            _globalKeyboardHook = new GlobalKeyboardHook();
            _globalKeyboardHook.KeyDown += new System.Windows.Forms.KeyEventHandler(ReboundKey_KeyDown);
            _globalKeyboardHook.KeyUp += new System.Windows.Forms.KeyEventHandler(ReboundKey_KeyUp);
            DisplayReboundApplications();
        }

        private void LoadRebinderInfo()
        {
            _rebinderInfoLookup.Clear();
            if (File.Exists(_savedRebindingsFileLocation))
            {
                IEnumerable<string> lines = File.ReadLines(_savedRebindingsFileLocation).Where(l => !string.IsNullOrWhiteSpace(l));

                foreach (string line in lines)
                {
                    RebinderInfo rbi = RebinderInfo.DeSerialize(line);
                    _rebinderInfoLookup.Add(rbi.ApplicationName, rbi);
                }
            }
        }

        private bool IsHiding
        {
            get => _isHiding;
            set
            {
                _isHiding = value;
                ReOpenMenuItem.IsEnabled = value;
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            if (_forceClose)
            {
                return;
            }

            e.Cancel = true;
            WindowState = WindowState.Minimized;
        }

        private static Process GetActiveWindowProcess()
        {
            _ = GetWindowThreadProcessId(GetForegroundWindow(), out uint processId);

            return Process.GetProcessById((int)processId);
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            switch (WindowState)
            {
                case WindowState.Minimized:
                    IsHiding = true;
                    Hide();
                    break;
                case WindowState.Normal:
                case WindowState.Maximized:
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(WindowState), (int)WindowState, typeof(WindowState));
            }
        }

        private void ForceClose(object sender, RoutedEventArgs e)
        {
            _forceClose = true;
            Close();
        }

        private void ReOpen(object sender, RoutedEventArgs e)
        {
            IsHiding = false;
            Show();
            WindowState = WindowState.Normal;
        }

        private void InitializeProcessMonitor()
        {
            BackgroundWorker currentlyRunningProcessWorker = new();

            currentlyRunningProcessWorker.DoWork += (_, _) =>
            {
                while (!_forceClose)
                {
                    using (Process activeProcess = GetActiveWindowProcess())
                    {
                        string appName = activeProcess.GetApplicationName();
                        Dispatcher?.Invoke(() =>
                        {
                            if (!string.Equals(_activeProcessName, appName))
                            {
                                _activeProcessName = appName;
                                CurrentProcessTxtBlk.Text = "Currently Focused: " + _activeProcessName;
                                if (_rebinderInfoLookup.TryGetValue(_activeProcessName, out RebinderInfo rebinderInfo))
                                {
                                    ActivateKeyRebindings(rebinderInfo);
                                }
                                else
                                {
                                    DeactivateKeyRebindings();
                                }
                            }
                        });
                    }

                    Thread.Sleep(1);
                }
            };

            currentlyRunningProcessWorker.RunWorkerAsync();
        }

        private void ActivateKeyRebindings(RebinderInfo rebinderInfo)
        {
            DeactivateKeyRebindings();
            _globalKeyboardHook.HookedKeys.Clear();
            foreach (KeyMapping keyMap in rebinderInfo.KeyMappings)
            {
                _globalKeyboardHook.HookedKeys.Add(keyMap.SourceKey);
            }
            _globalKeyboardHook.Hook();
        }

        private void DeactivateKeyRebindings()
        {
            _globalKeyboardHook.Unhook();
            _globalKeyboardHook.HookedKeys.Clear();
        }

        private void ReboundKey_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (_rebinderInfoLookup.TryGetValue(_activeProcessName, out RebinderInfo rebinderInfo))
            {
                System.Windows.Forms.Keys keyPressed = e.KeyCode;
                KeyMapping[] keyMappings = rebinderInfo.KeyMappings.Where(km => km.SourceKey == keyPressed).ToArray();

                e.Handled = keyMappings.Any();
                foreach (KeyMapping keyMap in keyMappings)
                {
                    if (keyMap.DestinationKey != System.Windows.Forms.Keys.None)
                    {
                        _ = _inputSimulator.Keyboard.KeyDown((VirtualKeyCode)keyMap.DestinationKey);
                    }
                }
            }
        }

        private void ReboundKey_KeyUp(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            if (_rebinderInfoLookup.TryGetValue(_activeProcessName, out RebinderInfo rebinderInfo))
            {
                System.Windows.Forms.Keys keyPressed = e.KeyCode;
                KeyMapping[] keyMappings = rebinderInfo.KeyMappings.Where(km => km.SourceKey == keyPressed).ToArray();

                e.Handled = keyMappings.Any();
                foreach (KeyMapping keyMap in keyMappings)
                {
                    if (keyMap.DestinationKey != System.Windows.Forms.Keys.None)
                    {
                        _ = _inputSimulator.Keyboard.KeyUp((VirtualKeyCode)keyMap.DestinationKey);
                    }
                }
            }
        }

        private void EditRebind(object sender, RoutedEventArgs e)
        {
            string appName = (string)((Button)sender).Tag;

            OpenNewRebindDialog(appName);
        }

        private void AddNewRebind(object sender, RoutedEventArgs e)
        {
            OpenNewRebindDialog(null);
        }

        private void SaveRebindings()
        {
            StringBuilder sb = new();
            foreach (RebinderInfo rebindInfo in _rebinderInfoLookup.Values)
            {
                _ = sb.AppendLine(rebindInfo.Serialize());
            }
            File.WriteAllText(_savedRebindingsFileLocation, sb.ToString());
        }

        private void OpenNewRebindDialog(string applicationName)
        {
            SetupKeyRebindDialog setupKeyRebindDialog = applicationName is null
                ? new SetupKeyRebindDialog(_rebinderInfoLookup)
                : new SetupKeyRebindDialog(_rebinderInfoLookup, _rebinderInfoLookup.TryGetValue(applicationName, out RebinderInfo rebinderInfo) ? rebinderInfo : new RebinderInfo());
            BackgroundWorker worker = new();

            worker.DoWork += (_, _) =>
            {
                while (setupKeyRebindDialog.Visibility == Visibility.Visible && !IsHiding && !_forceClose)
                {
                    Thread.Sleep(100);
                }

                Dispatcher?.Invoke(() =>
                {
                    RebinderInfo results = setupKeyRebindDialog.GetRebinderInfo();
                    if (results is not null)
                    {
                        if (_rebinderInfoLookup.ContainsKey(results.ApplicationName))
                        {
                            _rebinderInfoLookup[results.ApplicationName] = results;
                        }
                        else if (!string.IsNullOrWhiteSpace(results.ApplicationName))
                        {
                            _rebinderInfoLookup.Add(results.ApplicationName, results);
                        }
                        DisplayReboundApplications();
                        SaveRebindings();
                    }

                    setupKeyRebindDialog.Dispose();
                });
            };

            worker.RunWorkerAsync();
        }

        private void DisplayReboundApplications()
        {
            Dispatcher?.Invoke(() =>
            {
                Grid table = ReboundApplicationsTable;
                table.RowDefinitions.Clear();
                table.Children.Clear();

                RowDefinition reboundApplicationsHeaderRow = new() { Height = new GridLength(15) };
                table.RowDefinitions.Add(reboundApplicationsHeaderRow);

                TextBlock applicationNameHeader = new()
                {
                    Text = "Application Name",
                    FontSize = 10,
                    Padding = new Thickness(15, 0, 0, 0),
                    Background = Brushes.LightGray,
                };

                Grid.SetColumn(applicationNameHeader, 2);
                Grid.SetRow(applicationNameHeader, 0);

                _ = table.Children.Add(applicationNameHeader);
                string[] apps = _rebinderInfoLookup.Keys.ToArray();
                for (int i = 0; i < apps.Count(); i++)
                {
                    RowDefinition row = new() { Height = new GridLength(15) };
                    table.RowDefinitions.Add(row);

                    string applicationName = apps[i];
                    Button deleteButton = new()
                    {
                        Tag = applicationName,
                        Content = new Image { Source = _trashCanBitmap },
                    };
                    deleteButton.Click += (object sender, RoutedEventArgs e) =>
                    {
                        _ = _rebinderInfoLookup.Remove(applicationName);
                        SaveRebindings();
                        DisplayReboundApplications();
                    };

                    Button editButton = new()
                    {
                        Tag = applicationName,
                        Content = new TextBlock
                        {
                            Text = "Edit",
                            FontSize = 10,
                        },
                    };
                    editButton.Click += EditRebind;

                    TextBlock applicationNameText = new()
                    {
                        Text = applicationName,
                        FontSize = 10,
                        Padding = new Thickness(15, 0, 0, 0),
                    };

                    Grid.SetColumn(deleteButton, 0);
                    Grid.SetRow(deleteButton, i + 1);

                    Grid.SetColumn(editButton, 1);
                    Grid.SetRow(editButton, i + 1);

                    Grid.SetColumn(applicationNameText, 2);
                    Grid.SetRow(applicationNameText, i + 1);

                    _ = table.Children.Add(deleteButton);
                    _ = table.Children.Add(editButton);
                    _ = table.Children.Add(applicationNameText);
                }
            });
        }


        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    }
}
