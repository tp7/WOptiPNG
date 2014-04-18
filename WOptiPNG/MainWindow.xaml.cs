using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MahApps.Metro.Controls.Dialogs;

namespace WOptiPNG
{
    public partial class MainWindow
    {
        private readonly Settings _settings;
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _settings = Settings.ReadFromFile();
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

        protected override void OnClosed(EventArgs e)
        {
            _settings.WriteToFile();
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

        private void HandleFileEnterKeyUp(object sender, KeyEventArgs e)
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

        private void IgnoreFileEnterKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
            }
        }
    }
}
