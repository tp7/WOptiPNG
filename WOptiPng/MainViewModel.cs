using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using WOptiPng.Properties;

namespace WOptiPng
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
                return string.Format("Saved {0:###,###,###.##} KB", _saved / 1024.0f);
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

        private readonly ObservableCollection<OptimizationProcess> _files;
        public ReadOnlyObservableCollection<OptimizationProcess> Files { get; set; }

        private long _saved = 0;

        #endregion

        #region commands

        private RelayCommand _selectDirectoryCommand;

        public ICommand SelectDirectoryCommand
        {
            get
            {
                return _selectDirectoryCommand ?? (_selectDirectoryCommand = new RelayCommand(obj =>
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
                            OutputDirectory = ofd.FileName;
                        }
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
    }
}