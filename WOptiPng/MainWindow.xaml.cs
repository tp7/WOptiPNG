using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MahApps.Metro.Controls.Dialogs;
using Path = System.IO.Path;

namespace WOptiPng
{
    public partial class MainWindow
    {
        private readonly Settings _settings;
        private string _settingsPath;
        private readonly MainViewModel _viewModel;

        private string SettingsPath
        {
            get
            {
                if (_settingsPath == null)
                {
                    var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    _settingsPath = Path.Combine(appdata, "WOptiPng", "Settings.json");
                }
                return _settingsPath;
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            var serializer = new DataContractJsonSerializer(typeof (Settings));
            if (File.Exists(SettingsPath))
            {
                using (var stream = File.OpenRead(SettingsPath))
                {
                    _settings = (Settings)serializer.ReadObject(stream);
                }
            }
            else
            {
                _settings = new Settings();
                WriteSettings(SettingsPath, _settings);
            }

            _viewModel = new MainViewModel(_settings);
            DataContext = _viewModel;

            Loaded += async (sender, e) =>
            {
                if (!OptiPngWrapper.OptiPngExists())
                {
                    await this.ShowMessageAsync("OptiPng not found",
                            "It should be in PATH or program folder.\nThe application will now quit.");
                    Close();
                }
                if (_settings.SettingsValid())
                {
                    return;
                }
                var names = string.Join(", ", _settings.GetBrokenSettingsNames());
                var msg = string.Format("Some settings are broken ({0}), resetting to default", names);
                await this.ShowMessageAsync("Settings derped", msg);
                _settings.ResetBrokenSettings();
            };
        }
        
        private static void WriteSettings(string path, Settings value)
        {
            var folder = Path.GetDirectoryName(path);
            if (folder == null)
            {
                throw new IOException("Incorrect folder name");
            }
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            var serializer = new DataContractJsonSerializer(typeof (Settings));

            using (var stream = File.OpenWrite(path))
            {
                serializer.WriteObject(stream, value);
                stream.SetLength(stream.Position);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            WriteSettings(SettingsPath, _settings);
            base.OnClosed(e);
        }

        private void DataGridPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var grid = (DataGrid)sender;
            if (e.Key == Key.Delete)
            {
                var itemsToDelete = grid.SelectedItems.Cast<OptimizationProcess>().ToList();
                foreach (var row in itemsToDelete)
                {
                    _viewModel.DeleteFile(row);
                }
            }
        }

        private void SettingsButtonClick(object sender, RoutedEventArgs e)
        {
            SettingsView.IsOpen = !SettingsView.IsOpen;
        }

        private void DataGridPreviewDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var file in files)
                {
                    if (IsPng(file))
                    {
                        _viewModel.AddFile(file);
                    }
                    else if (_settings.IncludeSubfolders && IsDirectory(file))
                    {
                        foreach (var subFile in Directory.GetFiles(file, "*.png", SearchOption.AllDirectories))
                        {
                            _viewModel.AddFile(subFile);
                        }
                    }
                }
            }
        }

        private static bool IsPng(string path)
        {
            return path.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDirectory(string path)
        {
            var attrs = File.GetAttributes(path);
            return (attrs & FileAttributes.Directory) == FileAttributes.Directory;
        }

        private void HandleFileDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var row = (DataGridRow)sender;
            var op = (OptimizationProcess)row.Item;

            Process.Start(new ProcessStartInfo(op.InputPath));
            e.Handled = true;
        }

        private void HandleFileEnterPress(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }
            var row = (DataGridRow)sender;
            var op = (OptimizationProcess)row.Item;
            Process.Start(new ProcessStartInfo(op.InputPath));
            e.Handled = true;
        }
    }
}
