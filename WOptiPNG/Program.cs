using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace WOptiPNG
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                if (args.Any(f => "--background".Equals(f, StringComparison.OrdinalIgnoreCase)))
                {
                    var logPath = Path.Combine(Settings.ApplicationDataPath, "service.log");
                    var listener = new TextWriterTraceListener(logPath) {TraceOutputOptions = TraceOptions.DateTime};
                    Trace.AutoFlush = true;
                    Trace.Listeners.Add(listener);
                    new OptimizationService().Run();
                    return;
                }
            }

            App.Main();
        }
    }
}