using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
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
                if (IsAdministrator() && !EventLog.SourceExists(ServiceName))
                {
                    //yay, got admin rights for the first time, create the log
                    EventLog.CreateEventSource(ServiceName, "Application");
                }
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
                        ServiceBase.Run(new ServiceBase[] {new OptimizationService()});
                        return;
                    }
                }

                App.Main();
            }
            catch (Exception e)
            {
                WriteWindowsLog(e.Message, EventLogEntryType.Error);
            }
        }

        public static void WriteWindowsLog(string message, EventLogEntryType type)
        {
            Trace.WriteLine(message);
            try
            {
                EventLog.WriteEntry(ServiceName, message, type);
            }
            catch(Win32Exception)
            {
                //access denied, we've never run as administator
            }
        }

        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}