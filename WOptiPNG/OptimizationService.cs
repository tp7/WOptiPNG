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
    [System.ComponentModel.DesignerCategory("Code")]
    public class OptimizationService : ServiceBase
    {
        private readonly ConcurrentDictionary<FileSystemWatcher, WatchedDirectory> _watchers = new ConcurrentDictionary<FileSystemWatcher, WatchedDirectory>();
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
                Program.WriteWindowsLog(string.Format("Some settings are broken ({0}), using defaults", names),
                    EventLogEntryType.Warning);
                _settings.ResetBrokenSettings();
            }
        }

        void ReloadSettings()
        {
            Program.WriteWindowsLog("Reloading settings",EventLogEntryType.Information);

            LoadSettings();

            KillWatchers();
            CreateWatchers();
            KillThreads();
            CreateThreads(_settings.ServiceThreads);
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
                    Program.WriteWindowsLog(string.Format("Folder {0} not found", folder.Path),
                        EventLogEntryType.Warning);
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
                    var message = string.Format("Watching folder {0} from some other broader location", folder.Path);
                    Program.WriteWindowsLog(message, EventLogEntryType.Warning);
                    continue;
                }
                _watchers[CreatePngWatcher(folder.Path, folder.WatchSubfolders)] = folder;
            }
        }

        private void KillWatchers()
        {
            foreach (var watcher in _watchers.Keys)
            {
                watcher.Dispose();
            }
            _watchers.Clear();
        }

        protected override void OnStart(string[] args)
        {
            CreateWatchers();
            CreateThreads(_settings.ServiceThreads);
            base.OnStart(args);
        }
        private readonly List<Thread> _backgroundThreads = new List<Thread>();

        private void CreateThreads(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var thread = new Thread(() =>
                {
                    while (true)
                    {
                        Monitor.Enter(_filesToProcess);
                        string path = null;

                        try
                        {
                            while (_filesQueue.Count == 0)
                                Monitor.Wait(_filesToProcess);

                            path = _filesQueue.Dequeue();
                            Monitor.Exit(_filesToProcess);

                            int timeToWait;
                            do
                            {
                                timeToWait = 500 - (int)(DateTime.UtcNow - File.GetLastWriteTimeUtc(path)).TotalMilliseconds;
                                if (timeToWait <= 0 && IsFileLocked(path))
                                    timeToWait = 500;
                                if (timeToWait > 0)
                                    Thread.Sleep(timeToWait);
                            } while (timeToWait > 0);

                            try
                            {
                                ProcessFile(path, _settings);
                                Trace.WriteLine(string.Format("Successfully optimized file {0}", path));
                            }
                            catch (Exception e)
                            {
                                var message = string.Format("Error while processing files: {0}", e.Message);
                                Program.WriteWindowsLog(message, EventLogEntryType.Error);
                            }

                        }
                        finally
                        {
                            if (Monitor.IsEntered(_filesToProcess))
                            {
                                if (path != null)
                                    _filesToProcess.Remove(path);
                                Monitor.Exit(_filesToProcess);
                            }
                            else if (path != null)
                            {
                                lock (_filesToProcess)
                                    _filesToProcess.Remove(path);
                            }
                        }
                    }
                });
                _backgroundThreads.Add(thread);
                thread.Start();
            }
        }

        protected virtual bool IsFileLocked(string path)
        {
            FileStream stream = null;
            try
            {
                stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }
            return false;
        }

        private void KillThreads()
        {
            foreach (var thread in _backgroundThreads)
            {
                thread.Abort();
            }
            _backgroundThreads.Clear();
        }

        private static string ToCanonicalPath(string path)
        {
            return Path.GetFullPath(path).Replace('\\', '/').ToLowerInvariant();
        }

        protected override void OnStop()
        {
            KillThreads();
            KillWatchers();
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

        private readonly Queue<string> _filesQueue = new Queue<string>();
        private readonly HashSet<string> _filesToProcess = new HashSet<string>();

        private void PngFileCreated(object sender, FileSystemEventArgs e)
        {
            lock (_filesToProcess)
            {
                if (_filesToProcess.Contains(e.FullPath))
                    return;
                _filesToProcess.Add(e.FullPath);
                _filesQueue.Enqueue(e.FullPath);
                Monitor.Pulse(_filesToProcess);
            }
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