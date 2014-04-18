using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace WOptiPNG
{
    public class OptimizationService : ServiceBase
    {
        private readonly ConcurrentDictionary<FileSystemWatcher, WatchedDirectory> _watchers = new ConcurrentDictionary<FileSystemWatcher, WatchedDirectory>();
        private readonly ConcurrentQueue<string> _filesToProcess = new ConcurrentQueue<string>();
        private Settings _settings;
        private readonly FileSystemWatcher _settingsWatcher;
        private readonly ThrottledMethodCall _settingsReloader;
        
        public OptimizationService()
        {
            LoadSettings();
            var path = Settings.SettingsPath;
            _settingsWatcher = new FileSystemWatcher(Path.GetDirectoryName(path), Path.GetFileName(path));
            _settingsReloader = new ThrottledMethodCall(ReloadSettings, 500);
            _settingsWatcher.Changed += (sender, e) => _settingsReloader.Call();
            _settingsWatcher.EnableRaisingEvents = true;
        }

        private void LoadSettings()
        {
            _settings = Settings.ReadFromFile();
            if (!_settings.SettingsValid())
            {
                var names = string.Join(", ", _settings.GetBrokenSettingsNames());
                Trace.TraceWarning(string.Format("Some settings are broken ({0}), using defaults", names));
                _settings.ResetBrokenSettings();
            }
        }

        void ReloadSettings()
        {
            Trace.WriteLine("Reloading settings");
            
            LoadSettings();
            
            foreach (var watcher in _watchers.Keys)
            {
                watcher.Dispose();
            }
            _watchers.Clear();
            CreateWatchers();
        }

        private void CreateWatchers()
        {
            if (_settings.WatchedFolders == null || _settings.WatchedFolders.Count == 0)
            {
                return;
            }
            foreach (var folder in _settings.WatchedFolders)
            {
                if (!Directory.Exists(folder.Path))
                {
                    Trace.TraceWarning("Folder {0} doesn't exist", folder.Path);
                    continue;
                }

                //we assume that there won't be too many folders so this won't be too slow
                var canonicalPath = ToCanonicalPath(folder.Path);
                var alreadyWatched = _settings.WatchedFolders
                    .Any(other =>
                    {
                        if (other == folder)
                        {
                            return false;
                        }
                        var otherPath = ToCanonicalPath(other.Path);
                        return canonicalPath == otherPath || (canonicalPath.StartsWith(otherPath) && other.WatchSubfolders);
                    });
                if (alreadyWatched)
                {
                    Trace.TraceWarning("Watching folder {0} from some other broader location", folder.Path);
                    continue;
                }
                _watchers[CreatePngWatcher(folder.Path, folder.WatchSubfolders)] = folder;
            }
        }

        protected override void OnStart(string[] args)
        {
            CreateWatchers();
            base.OnStart(args);
        }

        private static string ToCanonicalPath(string path)
        {
            return Path.GetFullPath(path).Replace('\\', '/').ToLowerInvariant();
        }

        protected override void OnStop()
        {
            foreach (var watcher in _watchers.Keys)
            {
                watcher.Dispose();
            }
            _settingsWatcher.Dispose();
            base.OnStop();
        }

        private FileSystemWatcher CreatePngWatcher(string path, bool includeSubfolders)
        {
            var watcher = new FileSystemWatcher(path, "*.png");
            watcher.Created += PngFileCreated;
            watcher.Error += FileWatcherError;
            watcher.IncludeSubdirectories = includeSubfolders;
            watcher.EnableRaisingEvents = true;
            return watcher;
        }

        private void FileWatcherError(object sender, ErrorEventArgs e)
        {
            
            var watcher = (FileSystemWatcher)sender;
            watcher.Dispose();

            WatchedDirectory folder;
            if (_watchers.TryRemove(watcher, out folder))
            {
                _watchers[CreatePngWatcher(folder.Path, folder.WatchSubfolders)] = folder;
                Trace.WriteLine(string.Format("Watcher error on folder {0}. Error: {1}", folder.Path,
                    e.GetException().Message));
            }
            else
            {
                Trace.WriteLine(string.Format("Couldn't remove watcher, error: {0}", e.GetException().Message));
            }
        }

        private int _runningTasksCount;

        private void PngFileCreated(object sender, FileSystemEventArgs e)
        {
            _filesToProcess.Enqueue(e.FullPath);

            if (_runningTasksCount >= _settings.ServiceThreads)
            {
                return;
            }

            Interlocked.Increment(ref _runningTasksCount);

            Task.Run(() =>
            {
                string path;
                while (_filesToProcess.TryDequeue(out path))
                {
                    if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(path)) < TimeSpan.FromMilliseconds(500))
                    {
                        _filesToProcess.Enqueue(path);
                        Thread.Sleep(500);
                        continue;
                    }
                    ProcessFile(path, _settings);
                    Trace.WriteLine(string.Format("Successfully optimized file {0}", path));
                }
            }).ContinueWith(f =>
            {
                Interlocked.Decrement(ref _runningTasksCount);
                if (f.Exception != null)
                {
                    Trace.TraceError("Error while processing files: {0}", f.Exception.InnerException.Message);
                }
            });
        }

        private static void ProcessFile(string inputPath, Settings settings)
        {
            var tempFile = Path.GetTempFileName();
            var sizeBefore = new FileInfo(inputPath).Length;
            try
            {
                File.Copy(inputPath, tempFile, true);

                var result = OptiPngWrapper.Optimize(tempFile, settings.ServiceOptLevel, settings.ServiceProcessPriority, null);
                if (result != 0)
                {
                    return;
                }

                var newSize = new FileInfo(tempFile).Length;
                if (newSize >= sizeBefore)
                {
                    return;
                }

                if (settings.OverwriteSource)
                {
                    File.Copy(tempFile, inputPath, true);
                }
                else
                {
                    var oldName = Path.GetFileName(inputPath);
                    var newPath = Path.Combine(settings.OutputDirectory, oldName);
                    if (!File.Exists(newPath))
                    {
                        File.Copy(tempFile, newPath, false);
                    }
                }
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}