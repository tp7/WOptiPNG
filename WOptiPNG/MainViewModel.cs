using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using WOptiPNG.Properties;

namespace WOptiPNG
{
    //todo: probably separate this into two view models
    public class MainViewModel : BindableModel
    {
        #region properties

        #region settings

        private readonly Settings _settings;

        public bool OverwriteSource
        {
            get { return _settings.OverwriteSource; }
            set
            {
                if (value.Equals(_settings.OverwriteSource))
                {
                    return;
                }
                _settings.OverwriteSource = value;
                OnPropertyChanged();
                OnPropertyChanged("IsFolderSelectEnabled");
                OnPropertyChanged("StartButtonEnabled");
                OnPropertyChanged("StartButtonTooltip");
            }
        }

        public string OutputDirectory
        {
            get { return _settings.OutputDirectory; }
            set
            {
                if (value == _settings.OutputDirectory)
                {
                    return;
                }
                _settings.OutputDirectory = value;
                OnPropertyChanged();
                OnPropertyChanged("StartButtonEnabled");
                OnPropertyChanged("StartButtonTooltip");
            }
        }

        public int Threads
        {
            get { return _settings.Threads; }
            set
            {
                if (value == _settings.Threads)
                {
                    return;
                }
                _settings.Threads = value;
                OnPropertyChanged();
            }
        }

        public int OptLevel
        {
            get { return _settings.OptLevel; }
            set
            {
                if (value == _settings.OptLevel)
                {
                    return;
                }
                _settings.OptLevel = value;
                OnPropertyChanged();
            }
        }

        public bool IncludeSubfolders
        {
            get { return _settings.IncludeSubfolders; }
            set
            {
                if (value == _settings.IncludeSubfolders)
                {
                    return;
                }
                _settings.IncludeSubfolders = value;
                OnPropertyChanged();
            }
        }

        public ProcessPriorityClass ProcessPriority
        {
            get { return _settings.ProcessPriority; }
            set
            {
                if (value == _settings.ProcessPriority)
                {
                    return;
                }
                _settings.ProcessPriority = value;
                OnPropertyChanged();
            }
        }

        public int ServiceThreads
        {
            get { return _settings.ServiceThreads; }
            set
            {
                if (value == _settings.ServiceThreads)
                {
                    return;
                }
                _settings.ServiceThreads = value;
                OnPropertyChanged();
            }
        }

        public ProcessPriorityClass ServiceProcessPriority
        {
            get { return _settings.ServiceProcessPriority; }
            set
            {
                if (value == _settings.ServiceProcessPriority)
                {
                    return;
                }
                _settings.ServiceProcessPriority = value;
                OnPropertyChanged();
            }
        }

        public int ServiceOptLevel
        {
            get { return _settings.ServiceOptLevel; }
            set
            {
                if (value == _settings.ServiceOptLevel)
                {
                    return;
                }
                _settings.ServiceOptLevel = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<WatchedDirectory> WatchedFolders
        {
            get { return _settings.WatchedFolders; }
        }

        #endregion


        private bool _inProgress;
        public bool InProgress
        {
            get { return _inProgress; }
            private set
            {
                if (value.Equals(_inProgress)) return;
                _inProgress = value;
                OnPropertyChanged();
                OnPropertyChanged("IsFolderSelectEnabled");
                OnPropertyChanged("StartButtonEnabled");
                OnPropertyChanged("StartButtonTitle");
                OnPropertyChanged("StartButtonTooltip");
            }
        }

        public string StartButtonTooltip
        {
            get
            {
                if (Files.Count == 0)
                {
                    return "No files selected";
                }
                if (Files.All(f => f.IsDone))
                {
                    return "No files left to process";
                }
                if (!OverwriteSource && string.IsNullOrWhiteSpace(OutputDirectory))
                {
                    return "No output path selected";
                }
                return null;
            }
        }

        public object StartButtonTitle { get { return InProgress ? "Cancel" : "Start"; } }

        public string StatusMessage
        {
            get
            {
                if (_saved == 0)
                {
                    return null;
                }
                return string.Format(CultureInfo.CurrentCulture, "Saved {0:###,###,###.##} KB", _saved/1024.0f);
            }
        }

        public bool IsFolderSelectEnabled { get { return !(InProgress || OverwriteSource); }}

        public bool StartButtonEnabled
        {
            get
            {
                return Files.Count > 0 &&
                       Files.Any(f => !f.IsDone) &&
                       (OverwriteSource || !string.IsNullOrWhiteSpace(OutputDirectory));
            }
        }

        public string InstallServiceButtonText
        {
            get
            {
                return Program.ServiceInstalled ? "Uninstall service" : "Install service";
            }
        }


        private readonly ObservableCollection<OptimizationProcess> _files;
        public ReadOnlyObservableCollection<OptimizationProcess> Files { get; private set; }

        private long _saved;

        #endregion

        #region commands

        private RelayCommand _selectDirectoryCommand;

        public ICommand SelectDirectoryCommand
        {
            get
            {
                return _selectDirectoryCommand ?? (_selectDirectoryCommand = new RelayCommand(obj =>
                {
                    string temp;
                    if (SelectFolder(out temp))
                    {
                        OutputDirectory = temp;
                    }
                }));
            }
        }

        private RelayCommand _addFilesCommand;
        public ICommand AddFilesCommand { get
        {
            return _addFilesCommand ?? (_addFilesCommand = new RelayCommand((obj) =>
            {
                var ofd = new OpenFileDialog
                {
                    Multiselect = true,
                    RestoreDirectory = true,
                    Filter = "Png files|*.png"
                };
                if (ofd.ShowDialog() == true)
                {
                    foreach (var file in ofd.FileNames)
                    {
                        AddFile(file);
                    }
                }
            }));
        } }

        private RelayCommand _clearFilesCommand;
        public ICommand ClearFilesCommand
        {
            get
            {
                return _clearFilesCommand ?? (_clearFilesCommand = new RelayCommand(obj =>
                {
                    _files.Clear();
                    _saved = 0;
                    OnPropertyChanged("StatusMessage");
                }));
            }
        }

        private RelayCommand _startOrCancelCommand;
        public ICommand StartOrCancelCommand
        {
            get
            {
                return _startOrCancelCommand ?? (_startOrCancelCommand = new RelayCommand(obj =>
                {
                    if (InProgress)
                    {
                        CancelProcessing();
                    }
                    else
                    {
                        StartProcessing();
                    }
                }));
            }
        }

        private RelayCommand _addWatchedFolderCommand;
        public ICommand AddWatchedFolderCommand
        {
            get
            {
                return _addWatchedFolderCommand ?? (_addWatchedFolderCommand = new RelayCommand(obj =>
                {
                    string temp;
                    if (SelectFolder(out temp) &&
                        !WatchedFolders.Any(f => f.Path.Equals(temp, StringComparison.OrdinalIgnoreCase)))
                    {
                        WatchedFolders.Add(new WatchedDirectory {Path = temp, WatchSubfolders = false});
                    }
                }));
            }
        }

        private RelayCommand _installServiceCommand;
        public ICommand InstallServiceCommand
        {
            get
            {
                return _installServiceCommand ?? (_installServiceCommand = new RelayCommand(obj =>
                {
                    if (Program.ServiceInstalled)
                    {
                        Program.UninstallService();
                    }
                    else
                    {
                        Program.InstallAndStart();
                    }
                    OnPropertyChanged("InstallServiceButtonText");
                }));
            }
        }

        #endregion

        public MainViewModel(Settings settings)
        {
            _settings = settings;
            _files = new ObservableCollection<OptimizationProcess>();
            Files = new ReadOnlyObservableCollection<OptimizationProcess>(_files);
            _files.CollectionChanged += (sender, e) => OnPropertyChanged("StartButtonEnabled");
        }

        private CancellationTokenSource _cancelTokenSource;

        private async void StartProcessing()
        {
            if (_cancelTokenSource != null)
            {
                _cancelTokenSource.Dispose();
            }
            _cancelTokenSource = new CancellationTokenSource();
            InProgress = true;
            
            await Task.Factory.StartNew(() =>
            {
                try
                {
                    var actions = Files.Where(f => !f.IsDone).Select(f => (Action)delegate
                    {
                        f.Process();
                        if (f.SizeAfter != null)
                        {
                            var diff = f.SizeBefore - f.SizeAfter.GetValueOrDefault();
                            Interlocked.Add(ref _saved, diff);
                            OnPropertyChanged("StatusMessage");
                        }
                    });
                    //this method keeps processing order
                    Parallel.Invoke(new ParallelOptions
                    {
                        MaxDegreeOfParallelism = _settings.Threads,
                        CancellationToken = _cancelTokenSource.Token
                    }, actions.ToArray());
                }
                catch (OperationCanceledException)
                {
                    InProgress = false;
                }
            });

            InProgress = false;
        }

        private void CancelProcessing()
        {
            if (!_cancelTokenSource.IsCancellationRequested)
            {
                _cancelTokenSource.Cancel();
            }
        }

        public void DeleteFile(OptimizationProcess file)
        {
            if (file.Status != OptimizationProcessStatus.InProgress)
            {
                _files.Remove(file);
                if (file.SizeAfter != null)
                {
                    _saved -= file.SizeBefore - file.SizeAfter.GetValueOrDefault();
                    OnPropertyChanged("StatusMessage");
                }
            }
        }

        public void AddFile(string path)
        {
            if (_files.Any(f => f.InputPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
            {
                //ignore already added files. Put paths in a hashset if this is too slow
                return;
            }
            _files.Add(new OptimizationProcess(path, _settings));
        }

        private bool SelectFolder(out string path)
        {
            using (var ofd = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                InitialDirectory = OutputDirectory,
                Multiselect = false
            })
            {
                if (ofd.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    path = ofd.FileName;
                    return true;
                }
                path = null;
                return false;
            }
        }
    }
}