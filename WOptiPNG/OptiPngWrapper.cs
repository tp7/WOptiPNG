using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace WOptiPNG
{
    public static class OptiPngWrapper
    {
        public static bool OptiPngExists()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo("optipng")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            try
            {
                process.Start();
            }
            catch (Win32Exception)
            {
                return false;
            }
            return true;
        }

        public static int Optimize(string filePath, Settings settings,
            Action<string> standardErrorCallback)
        {
            using (var p = new Process
            {
                StartInfo = new ProcessStartInfo("optipng")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = FormatArguments(filePath, settings.OptLevel),
                    RedirectStandardError = true,
                }
            })
            {
                p.Start();
                p.PriorityClass = settings.ProcessPriority;
                p.ErrorDataReceived += (sender, e) =>
                {
                    if (standardErrorCallback != null)
                    {
                        standardErrorCallback(e.Data);
                    }
                };
                p.BeginErrorReadLine();
                p.WaitForExit();
                return p.ExitCode;
            }
        }

        private static string FormatArguments(string outputPath, int optLevel)
        {
            return string.Format(CultureInfo.InvariantCulture, "-clobber -preserve -fix -o {0} \"{1}\"",
                optLevel, outputPath);
        }
    }
}