using System;
using System.Windows;
using System.Windows.Threading;

namespace ShoeStoreApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            base.OnStartup(e);
        }

        private static void App_DispatcherUnhandledException(
            object sender,
            DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                "В приложении произошла непредвиденная ошибка. " +
                "Повторите действие. Если ошибка сохранится, перезапустите приложение.\n\n" +
                "Подробности: " + e.Exception.Message,
                "Ошибка приложения",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            e.Handled = true;
        }
    }
}
