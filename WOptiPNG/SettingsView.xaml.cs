using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace WOptiPNG
{
    public partial class SettingsView
    {
        public SettingsView()
        {
            InitializeComponent();
            for (int i = 1; i <= Environment.ProcessorCount*2; i++)
            {
                ThreadsBox.Items.Add(i);
                ServiceThreadsBox.Items.Add(i);
            }

            //don't allow real time because that's just wrong
            foreach (var c in new[] {ServiceAllowedPriorities, AllowedPriorities})
            {
                c.Items.Add(ProcessPriorityClass.Idle);
                c.Items.Add(ProcessPriorityClass.BelowNormal);
                c.Items.Add(ProcessPriorityClass.Normal);
                c.Items.Add(ProcessPriorityClass.AboveNormal);
                c.Items.Add(ProcessPriorityClass.High);
            }

            if (!IsAdministrator())
            {
                InstallServiceButton.ToolTip = "You have to run the app as administrator";
                InstallServiceButton.IsEnabled = false;
            }
        }

        private void HandleNavigationRequest(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
