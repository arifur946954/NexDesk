/*namespace RemoteDesktopClient
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

namespace RemoteDesktopClient
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the Client application.
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

            // Start the Client form
            Application.Run(new Form1());
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"Unexpected error: {e.ExceptionObject}", "Client Error",
                          MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}