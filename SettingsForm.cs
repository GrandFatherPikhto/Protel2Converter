using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace FolderWatcher.Service
{
    public partial class SettingsForm : Form
    {
        private readonly string _configPath;
        private ListBox _listBox = null!;
        private Button _addButton = null!;
        private Button _removeButton = null!;
        private Button _saveButton = null!;
        private IConfigurationRoot _configuration = null!;

        public SettingsForm()
        {
            InitializeComponent();
            // _configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            // _configPath = Path.Combine(exeDir, "appsettings.json");
            // Гарантированно берем папку, где лежит сам файл NetFileConverter.exe
            string exePath = System.Environment.ProcessPath ?? AppContext.BaseDirectory;
            string exeDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
            _configPath = Path.Combine(exeDir, "appsettings.json");            
            LoadSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "Настройки Folder Watcher";
            this.Size = new Size(400, 300);
            this.StartPosition = FormStartPosition.CenterScreen;

            _listBox = new ListBox { Dock = DockStyle.Fill };
            _addButton = new Button { Text = "Добавить папку", Dock = DockStyle.Bottom, Height = 30 };
            _removeButton = new Button { Text = "Удалить", Dock = DockStyle.Bottom, Height = 30 };
            _saveButton = new Button { Text = "Сохранить", Dock = DockStyle.Bottom, Height = 30 };

            var panel = new Panel { Dock = DockStyle.Bottom, Height = 90 };
            panel.Controls.Add(_addButton);
            panel.Controls.Add(_removeButton);
            panel.Controls.Add(_saveButton);
            _addButton.Top = 0;
            _removeButton.Top = 30;
            _saveButton.Top = 60;

            this.Controls.Add(_listBox);
            this.Controls.Add(panel);

            _addButton.Click += AddButton_Click;
            _removeButton.Click += RemoveButton_Click;
            _saveButton.Click += SaveButton_Click;
        }

        private void LoadSettings()
        {
            string exePath = System.Environment.ProcessPath ?? AppContext.BaseDirectory;
            string exeDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;

            // Перезагружаем конфигурацию из файла
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            _configuration = builder.Build();

            var directories = _configuration.GetSection("WatcherSettings:SourceDirectories").Get<string[]>() ?? Array.Empty<string>();
            _listBox.Items.Clear();
            _listBox.Items.AddRange(directories);
        }

        private void AddButton_Click(object? sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog();
            dialog.Description = "Выберите папку для отслеживания";
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                if (!_listBox.Items.Contains(dialog.SelectedPath))
                    _listBox.Items.Add(dialog.SelectedPath);
            }
        }

        private void RemoveButton_Click(object? sender, EventArgs e)
        {
            if (_listBox.SelectedItem != null)
                _listBox.Items.Remove(_listBox.SelectedItem);
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            try
            {
                var directories = _listBox.Items.Cast<string>().ToArray();
                // Читаем текущий JSON
                string json = File.ReadAllText(_configPath);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                using var stream = new MemoryStream();
                using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
                writer.WriteStartObject();

                foreach (var property in root.EnumerateObject())
                {
                    if (property.Name == "WatcherSettings")
                    {
                        writer.WriteStartObject("WatcherSettings");
                        writer.WriteStartArray("SourceDirectories");
                        foreach (var dir in directories)
                            writer.WriteStringValue(dir);
                        writer.WriteEndArray();
                        // копируем остальные поля WatcherSettings, если они есть
                        foreach (var innerProp in property.Value.EnumerateObject())
                        {
                            if (innerProp.Name != "SourceDirectories")
                                innerProp.WriteTo(writer);
                        }
                        writer.WriteEndObject();
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }
                writer.WriteEndObject();
                writer.Flush();
                File.WriteAllBytes(_configPath, stream.ToArray());

                // Уведомляем фоновую службу об изменениях – через IConfiguration reload
                // Worker уже подписан на GetReloadToken, он сам перезапустит наблюдателей
                MessageBox.Show("Настройки сохранены. Сервис автоматически применит их через несколько секунд.", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}