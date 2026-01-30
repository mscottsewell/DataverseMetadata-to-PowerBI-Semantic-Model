using System;
using System.Windows.Forms;
using DataverseToPowerBI.Configurator.Forms;

namespace DataverseToPowerBI.Configurator
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
