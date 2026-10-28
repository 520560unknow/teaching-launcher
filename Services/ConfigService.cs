using System.IO;
using System.Text.Json;
using StartMode.Models;

namespace StartMode.Services
{
    public class ConfigService
    {
        private readonly string _configPath;
        private AppConfig _config;

        public ConfigService()
        {
            _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            _config = LoadConfig();
        }

        public AppConfig Config => _config;

        private AppConfig LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    return JsonSerializer.Deserialize<AppConfig>(json, options) ?? new AppConfig();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载配置失败: {ex.Message}");
            }
            return new AppConfig();
        }

        public void SaveConfig()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_config, options);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存配置失败: {ex.Message}");
            }
        }

        public void UpdateConfig(Action<AppConfig> updateAction)
        {
            updateAction(_config);
            SaveConfig();
        }
    }
}
