using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;

namespace WOptiPNG
{
    public static class Program
    {
        private const string ServiceName = "WOptiPNGService";

        public static bool ServiceInstalled { get { return ServiceInstaller.ServiceIsInstalled(ServiceName); } }

        public static void InstallAndStart()
        {
            var command = string.Format("{0} --background", Assembly.GetExecutingAssembly().Location);
            ServiceInstaller.InstallAndStart(ServiceName, "WOptiPNG File Watcher", command);
        }

        public static void UninstallService()
        {
            ServiceInstaller.Uninstall(ServiceName);
        }

        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                if (args != null && args.Length > 0)
                {
                    if (args.Any(f => "--install".Equals(f, StringComparison.OrdinalIgnoreCase)))
                    {
                        InstallAndStart();
                        return;
                    }
                    if (args.Any(f => "--uninstall".Equals(f, StringComparison.OrdinalIgnoreCase)))
                    {
                        UninstallService();
                        return;
                    }
                    if (args.Any(f => "--background".Equals(f, StringComparison.OrdinalIgnoreCase)))
                    {
                        var logPath = Path.Combine(Settings.ApplicationDataPath, "service.log");
                        var listener = new TextWriterTraceListener(logPath) {TraceOutputOptions = TraceOptions.DateTime};
                        Trace.AutoFlush = true;
                        Trace.Listeners.Add(listener);
                        // Change the following line to match.
                        ServiceBase.Run(new ServiceBase[] {new OptimizationService()});
                        //new OptimizationService().Run();
                        return;
                    }
                }

                App.Main();
            }
            catch (Exception e)
            {
                Trace.TraceError(e.Message);
            }
        }
    }
}