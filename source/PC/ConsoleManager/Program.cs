using Dark.Net;
using System;
using System.Windows.Forms;

namespace CM
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
           // new1 ConsoleManager = new new1();
            App ConsoleManager = new App();
            DarkNet.Instance.SetWindowThemeForms(ConsoleManager, Theme.Auto);
            DarkNet.Instance.SetCurrentProcessTheme(Theme.Auto);
            Application.Run(ConsoleManager);
        }
    }
}
