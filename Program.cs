using System;
using System.Windows.Forms;
using System.Threading;

namespace Informer
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Обработка необработанных исключений
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
            {
                // Логируем ошибку, но не прерываем работу приложения
                System.Diagnostics.Debug.WriteLine("Необработанное исключение: " + e.Exception.Message);
            };
            
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                // Логируем критическую ошибку
                System.Diagnostics.Debug.WriteLine("Критическое исключение: " + (e.ExceptionObject as Exception)?.Message);
            };
            
            try
            {
                // Включаем визуальные стили
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                // Загружаем настройки
                Settings.LoadSettings();
                
                // Запускаем приложение
                Application.Run(new SystemInfoOverlay());
            }
            catch (Exception ex)
            {
                // Критическая ошибка при запуске
                MessageBox.Show("Критическая ошибка при запуске приложения:\n" + ex.Message, 
                    "Informer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
} 