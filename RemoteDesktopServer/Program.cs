/*namespace RemoteDesktopServer
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
    }
}*/

using System;
using System.Windows.Forms;

namespace RemoteDesktopServer
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the Server application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Enable visual styles for modern UI
            Application.EnableVisualStyles();

            // Set compatible text rendering
            Application.SetCompatibleTextRenderingDefault(false);

            // Handle any unhandled exceptions
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            //Start 

            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                Environment.Exit(0);
            };
            //End 
            // Start the Server form
            Application.Run(new Form1());
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"Unexpected error: {e.ExceptionObject}", "Server Error",
                          MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}