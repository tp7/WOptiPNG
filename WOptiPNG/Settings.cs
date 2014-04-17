using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Json;

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
        }

        public bool OverwriteSource { get; set; }
        public string OutputDirectory { get; set; }
        public int Threads { get; set; }
        public int OptLevel { get; set; }
        public bool IncludeSubfolders { get; set; }
        public ProcessPriorityClass ProcessPriority { get; set; }

        private static int DefaultThreads { get { return Environment.ProcessorCount; } }
        private static int DefaultOptLevel { get { return 2; } }

        //windows service settings
        public ICollection<WatchedDirectory> WatchedFolders { get; set; }
        public int BackgroundThreads { get; set; }

        public bool SettingsValid()
        {
            return Threads > 0 && OptLevel > 0 && OptLevel <= 8;
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
            var serializer = new DataContractJsonSerializer(typeof(Settings));

            using (var stream = File.OpenWrite(SettingsPath))
            {
                serializer.WriteObject(stream, this);
                stream.SetLength(stream.Position);
            }
        }

        private static string _settingsPath;
        private static string SettingsPath
        {
            get { return _settingsPath ?? (_settingsPath = Path.Combine(ApplicationDataPath, "Settings.json")); }
        }

        public static string ApplicationDataPath
        {
            get
            {
                var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appdata, "WOptiPng");
            }
        }

        public static Settings ReadFromFile()
        {
            var serializer = new DataContractJsonSerializer(typeof (Settings));

            if (File.Exists(SettingsPath))
            {
                using (var stream = File.OpenRead(SettingsPath))
                {
                    return (Settings)serializer.ReadObject(stream);
                }
            }
            var settings = new Settings();
            settings.WriteToFile();
            return settings;
        }
    }
}