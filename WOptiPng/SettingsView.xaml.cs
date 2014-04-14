using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WOptiPng
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            for (int i = 1; i <= Environment.ProcessorCount*2; i++)
            {
                ThreadsBox.Items.Add(i);
            }

            //don't allow real time because that's just wrong
            AllowedPriorities.Items.Add(ProcessPriorityClass.Idle);
            AllowedPriorities.Items.Add(ProcessPriorityClass.BelowNormal);
            AllowedPriorities.Items.Add(ProcessPriorityClass.Normal);
            AllowedPriorities.Items.Add(ProcessPriorityClass.AboveNormal);
            AllowedPriorities.Items.Add(ProcessPriorityClass.High);
        }
    }
}
