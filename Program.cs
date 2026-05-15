using System;
using System.Drawing;
using System.Windows.Forms;
using FolderWatcher.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FolderWatcher
{
    internal static class Program
    {
        private static IHost? _host;
        private static NotifyIcon? _trayIcon;
        private static SettingsForm? _settingsForm;

        [STAThread]
        static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            
            // Получаем гарантированный путь к папке с .exe
            string exePath = System.Environment.ProcessPath ?? AppContext.BaseDirectory;
            string exeDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
            
            // Принудительно меняем текущую рабочую директорию процесса
            Directory.SetCurrentDirectory(exeDir);

            // 1. Создаем builder для .NET 8
            var builder = Host.CreateApplicationBuilder(args);
            
            // 2. Исправление ошибки: регистрируем сервисы напрямую через .Services
            builder.Services.AddHostedService<Worker>();
            
            // Регистрируем форму в DI-контейнере, чтобы она могла принимать IConfiguration или логи
            builder.Services.AddTransient<SettingsForm>();

            // 3. Собираем хост
            _host = builder.Build();

            // Безопасно запускаем фоновые службы хоста (Worker)
            _host.StartAsync().GetAwaiter().GetResult();

            // Настройка Tray Icon
            _trayIcon = new NotifyIcon
            {
                // Извлекаем главную встроенную иконку самого .exe файла
                Icon = Icon.ExtractAssociatedIcon(System.Environment.ProcessPath ?? AppContext.BaseDirectory),
                Visible = true,
                Text = "NetList Folder Watcher Service"
            };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Настройки", null, ShowSettings);
            contextMenu.Items.Add("Выход", null, ExitApplication);
            _trayIcon.ContextMenuStrip = contextMenu;
            _trayIcon.DoubleClick += ShowSettings;

            // Запуск цикла обработки сообщений Windows
            Application.Run();

            // Корректное завершение работы
            if (_host != null)
            {
                _host.StopAsync().GetAwaiter().GetResult();
                _host.Dispose();
            }
            _trayIcon.Dispose();
        }

        // Метод для вызова всплывающих подсказок из любого места программы
        public static void ShowNotification(string title, string text, ToolTipIcon iconType = ToolTipIcon.Info)
        {
            // Проверяем, что иконка в трее инициализирована и запущена в UI-потоке
            if (_trayIcon != null && _trayIcon.Visible)
            {
                // Вызываем подсказку: (время показа в мс, заголовок, текст, иконка типа Info/Warning/Error)
                _trayIcon.ShowBalloonTip(3000, title, text, iconType);
            }
        }        

        private static void ShowSettings(object? sender, EventArgs e)
        {
            if (_settingsForm == null || _settingsForm.IsDisposed)
            {
                // Запрашиваем форму из DI-контейнера хоста
                _settingsForm = _host?.Services.GetRequiredService<SettingsForm>() ?? new SettingsForm();
            }
            _settingsForm.Show();
            _settingsForm.Activate();
        }

        private static void ExitApplication(object? sender, EventArgs e)
        {
            Application.Exit();
        }
    }
}
