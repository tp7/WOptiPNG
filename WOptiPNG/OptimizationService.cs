using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WOptiPNG
{
    public class OptimizationService : ServiceBase
    {
        private readonly ConcurrentDictionary<FileSystemWatcher, WatchedDirectory> _watchers = new ConcurrentDictionary<FileSystemWatcher, WatchedDirectory>();
        private readonly ConcurrentQueue<string> _filesToProcess = new ConcurrentQueue<string>();
        private readonly Settings _settings;

        public OptimizationService()
        {
            _settings = Settings.ReadFromFile();
        }

        protected override void OnStart(string[] args)
        {
            if (_settings.WatchedFolders == null || _settings.WatchedFolders.Count == 0)
            {
                return;
            }
            foreach (var folder in _settings.WatchedFolders)
            {
                Trace.WriteLine(folder.Path);
                _watchers[CreatePngWatcher(folder.Path, folder.WatchSubfolders)] = folder;
            }
            base.OnStart(args);
        }

        protected override void OnStop()
        {
            foreach (var watcher in _watchers.Keys)
            {
                watcher.Dispose();
            }

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
            Trace.WriteLine("Got watcher error");
            var watcher = (FileSystemWatcher)sender;
            watcher.Dispose();

            WatchedDirectory folder;
            if (_watchers.TryRemove(watcher, out folder))
            {
                _watchers[CreatePngWatcher(folder.Path, folder.WatchSubfolders)] = folder;
            }
            else
            {
                Trace.WriteLine("Couldn't remove watcher");
            }
        }

        private int _runningTasksCount;

        private void PngFileCreated(object sender, FileSystemEventArgs e)
        {
            Trace.WriteLine("File created");
            _filesToProcess.Enqueue(e.FullPath);

            if (_runningTasksCount >= 2)
            {
                return;
            }

            Interlocked.Increment(ref _runningTasksCount);

            Task.Run(() =>
            {
                string path;
                while (_filesToProcess.TryDequeue(out path))
                {
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

                if (OptiPngWrapper.Optimize(tempFile, settings, null) != 0)
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