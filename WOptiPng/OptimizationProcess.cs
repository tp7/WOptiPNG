using System;
using System.IO;

namespace WOptiPng
{
    public enum OptimizationProcessStatus
    {
        NotStarted,
        InProgress,
        Done,
        DoneButSizeIsLarger,
        Error
    }

    public class OptimizationProcess : BindableModel
    {
        private readonly Settings _settings;
        
        private long _sizeBefore;
        private long? _sizeAfter;
        private string _log;
        private OptimizationProcessStatus _status;

        public string InputPath { get; set; }

        public OptimizationProcessStatus Status
        {
            get { return _status; }
            private set
            {
                if (_status == value)
                {
                    return;
                }
                _status = value;
                OnPropertyChanged();
            }
        }

        public long SizeBefore
        {
            get { return _sizeBefore; }
            private set
            {
                if (value == _sizeBefore)
                {
                    return;
                }
                _sizeBefore = value;
                OnPropertyChanged();
            }
        }

        public long? SizeAfter
        {
            get { return _sizeAfter; }
            private set
            {
                if (value == _sizeAfter)
                {
                    return;
                }
                _sizeAfter = value;
                OnPropertyChanged();
                OnPropertyChanged("ReductionPercent");
            }
        }

        public float? ReductionPercent
        {
            get
            {
                if (SizeAfter == null)
                {
                    return null;
                }
                return (float)Math.Round((SizeBefore - SizeAfter.GetValueOrDefault()) / (double)SizeBefore * 100.0, 1);
            }
        }

        public string Log
        {
            get { return _log; }
            private set
            {
                if (value == _log)
                {
                    return;
                }
                _log = value;
                OnPropertyChanged();
            }
        }

        public bool IsDone
        {
            get
            {
                return _status == OptimizationProcessStatus.Done ||
                       _status == OptimizationProcessStatus.DoneButSizeIsLarger ||
                       _status == OptimizationProcessStatus.Error;
            }
        }

        public OptimizationProcess(string inputPath, Settings settings)
        {
            Status = OptimizationProcessStatus.NotStarted;
            _settings = settings;
            InputPath = inputPath;
            Log = "Nothing here yet";
            SizeBefore = new FileInfo(inputPath).Length;
        }

        public void Process()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                Status = OptimizationProcessStatus.InProgress;
                var status = OptiPngWrapper.Optimize(InputPath, tempFile, _settings, (str) => Log += str + '\n');
                
                if (status == 0)
                {
                    var newSize = new FileInfo(tempFile).Length;
                    if (newSize < SizeBefore)
                    {
                        if (_settings.OverwriteSource)
                        {
                            File.Copy(tempFile, InputPath, true);
                            Status = OptimizationProcessStatus.Done;
                        }
                        else
                        {
                            var oldName = Path.GetFileName(InputPath);
                            var newPath = Path.Combine(_settings.OutputDirectory, oldName);
                            if (File.Exists(newPath))
                            {
                                Log += string.Format("File {0} already exists", newPath);
                                Status = OptimizationProcessStatus.Error;
                            }
                            else
                            {
                                File.Copy(tempFile, newPath, false);
                                Status = OptimizationProcessStatus.Done;
                            }
                        }
                        SizeAfter = newSize;
                    }
                    else
                    {
                        SizeAfter = SizeBefore;
                        Status = OptimizationProcessStatus.DoneButSizeIsLarger;
                        Log += string.Format("Size after optimization is larger than before");
                    }
                }
                else
                {
                    Status = OptimizationProcessStatus.Error;
                }
            }
            catch (Exception)
            {
                Status = OptimizationProcessStatus.Error;
            }
            finally
            {
                File.Delete(tempFile);
                
            }
        }
    }
}