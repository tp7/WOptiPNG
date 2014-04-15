using System;
using System.ComponentModel;
using System.Diagnostics;

namespace WOptiPng
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

        public static int Optimize(string inputPath, string outputPath, Settings settings,
            Action<string> standardErrorCallback)
        {
            using (var p = new Process
            {
                StartInfo = new ProcessStartInfo("optipng")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = FormatArguments(outputPath, inputPath, settings.OptLevel),
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

        private static string FormatArguments(string outputPath, string inputPath, int optLevel)
        {
            return string.Format("-clobber -preserve -fix -out \"{0}\" -o {1} \"{2}\"", outputPath, optLevel, inputPath);
        }
    }
}