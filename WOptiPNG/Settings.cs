using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Json;

namespace WOptiPNG
{
    public class Settings
    {
        public Settings()
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

        public static int DefaultThreads { get { return Environment.ProcessorCount; } }
        public static int DefaultOptLevel { get { return 2; } }

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


        public void WriteToFile(string path)
        {
            var folder = Path.GetDirectoryName(path);
            if (folder == null)
            {
                throw new IOException("Incorrect folder name");
            }
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            var serializer = new DataContractJsonSerializer(typeof(Settings));

            using (var stream = File.OpenWrite(path))
            {
                serializer.WriteObject(stream, this);
                stream.SetLength(stream.Position);
            }
        }

        public static Settings ReadFromFile(string path)
        {
            var serializer = new DataContractJsonSerializer(typeof (Settings));

            if (File.Exists(path))
            {
                using (var stream = File.OpenRead(path))
                {
                    return (Settings)serializer.ReadObject(stream);
                }
            }
            var settings = new Settings();
            settings.WriteToFile(path);
            return settings;
        }
    }
}