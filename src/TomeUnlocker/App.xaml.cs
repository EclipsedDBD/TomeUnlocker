using System.Windows;

namespace TomeUnlocker
{
    public partial class App : Application
    {
        public static void ShutdownApplication(int exitCode)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Application.Current.Shutdown(exitCode);
            });
        }
    }
}
