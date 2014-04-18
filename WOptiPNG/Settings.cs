using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using Newtonsoft.Json;

namespace WOptiPNG
{
    public class WatchedDirectory
    {
        public string Path { get; set; }
        public bool WatchSubfolders { get; set; }
    }

    public class Settings
    {
        private Settings()
        {
            OverwriteSource = true;
            Threads = DefaultThreads;
            OptLevel = DefaultOptLevel;
            ProcessPriority = ProcessPriorityClass.Normal;
            ServiceThreads = DefaultServiceThreads;
            ServiceOptLevel = DefaultOptLevel;
            ServiceProcessPriority = ProcessPriorityClass.Normal;
        }

        public bool OverwriteSource { get; set; }
        public string OutputDirectory { get; set; }
        public int Threads { get; set; }
        public int OptLevel { get; set; }
        public bool IncludeSubfolders { get; set; }
        public ProcessPriorityClass ProcessPriority { get; set; }

        private static int DefaultThreads { get { return Environment.ProcessorCount; } }
        private static int DefaultOptLevel { get { return 2; } }
        private static int DefaultServiceThreads { get { return Math.Max(1, DefaultThreads/4); } }

        //windows service settings
        public ObservableCollection<WatchedDirectory> WatchedFolders { get; set; }
        public int ServiceThreads { get; set; }
        public ProcessPriorityClass ServiceProcessPriority { get; set; }
        public int ServiceOptLevel { get; set; }

        public bool SettingsValid()
        {
            return Threads > 0 && ServiceThreads > 0 && 
                OptLevel > 0 && OptLevel <= 8 &&
                ServiceOptLevel > 0 && ServiceOptLevel <= 8;
        }

        public void ResetBrokenSettings()
        {
            if (Threads <= 0)
            {
                Threads = DefaultThreads;
            }
            if (OptLevel <= 0 || OptLevel > 8)
            {
                OptLevel = DefaultOptLevel;
            }
            if (ServiceThreads <= 0)
            {
                ServiceThreads = DefaultServiceThreads;
            }
            if (ServiceOptLevel <= 0 || ServiceOptLevel > 8)
            {
                ServiceOptLevel = DefaultOptLevel;
            }
        }

        public IEnumerable<string> GetBrokenSettingsNames()
        {
            if (SettingsValid())
            {
                return new string[0];
            }
            var names = new List<string>();
            if (Threads <= 0)
            {
                names.Add("threads");
            }
            if (OptLevel <= 0 || OptLevel > 8)
            {
                names.Add("optimization level");
            }
            if (ServiceThreads <= 0)
            {
                names.Add("service threads");
            }
            if (ServiceOptLevel <= 0 || ServiceOptLevel > 8)
            {
                names.Add("service optimization level");
            }
            return names;
        }

        public void WriteToFile()
        {
            var folder = Path.GetDirectoryName(SettingsPath);
            if (folder == null)
            {
                throw new IOException("Incorrect folder name");
            }
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            FilterOutNonexistentFolders();
            var str = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(SettingsPath, str);
        }

        private static string _settingsPath;
        public static string SettingsPath
        {
            get { return _settingsPath ?? (_settingsPath = Path.Combine(ApplicationDataPath, "settings.json")); }
        }

        public static string ApplicationDataPath
        {
            get
            {
                var appdata = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                return Path.Combine(appdata, "WOptiPng");
            }
        }

        public static Settings ReadFromFile()
        {
            if (File.Exists(SettingsPath))
            {
                var text = File.ReadAllText(SettingsPath);
                var deserialised = JsonConvert.DeserializeObject<Settings>(text);
                deserialised.FilterOutNonexistentFolders();
                return deserialised;
            }
            var settings = new Settings();
            settings.WriteToFile();
            return settings;
        }

        private void FilterOutNonexistentFolders()
        {
            if (WatchedFolders == null)
            {
                return;
            }
            foreach (var wf in WatchedFolders.ToList())
            {
                if (!Directory.Exists(wf.Path))
                {
                    WatchedFolders.Remove(wf);
                }
            }
        }
    }
}