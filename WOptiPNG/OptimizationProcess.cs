using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace WOptiPNG
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

        public double? ReductionPercent
        {
            get
            {
                if (SizeAfter == null)
                {
                    return null;
                }
                return (SizeBefore - SizeAfter.GetValueOrDefault()) / (double)SizeBefore * 100.0;
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

        private static readonly Regex OptiPngTry = new Regex(@"\s*zc\s+=\s+\d+\s+zm\s+=\s+\d+\s+zs\s+=\s+\d+\s+f\s+=\s+\d+.*", RegexOptions.Compiled);

        public void Process()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                Status = OptimizationProcessStatus.InProgress;
                Log = null;

                File.Copy(InputPath, tempFile, true);

                int triesCount = 0;
                int lastLineLength = 0;
                var status = OptiPngWrapper.Optimize(tempFile, _settings, (str) =>
                {
                    if (str == null)
                    {
                        return;
                    }

                    var addition = str + '\n';
                    if (OptiPngTry.IsMatch(str))
                    {
                        if (triesCount < 14)
                        {
                            Log += addition;
                        }
                        else if (triesCount == 14)
                        {
                            var spaces = new string(addition.TakeWhile(char.IsWhiteSpace).ToArray());
                            Log += spaces + "...\n" + addition;
                        }
                        else
                        {
                            Log = Log.Remove(Log.Length - lastLineLength) + addition;
                        }
                        triesCount++;
                        lastLineLength = addition.Length;
                    }
                    else
                    {
                        triesCount = 0;
                        Log += addition;
                    }
                });
                
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
                                Log += string.Format(CultureInfo.CurrentCulture, "File {0} already exists", newPath);
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
                        Log += "Size after optimization is larger than before";
                    }
                }
                else
                {
                    Status = OptimizationProcessStatus.Error;
                }
            }
            catch (Exception e)
            {
                Status = OptimizationProcessStatus.Error;
                Log = e.Message;
            }
            finally
            {
                File.Delete(tempFile);
                
            }
        }
    }
}