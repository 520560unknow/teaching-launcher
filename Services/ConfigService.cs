using System.IO;
using System.Text.Json;
using StartMode.Models;

namespace StartMode.Services
{
    public class ConfigService
    {
        private readonly string _configPath;
        private AppConfig _config;
        private bool _loadFailed;

        public ConfigService(string? configPath = null)
        {
            _configPath = configPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
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
                    var loaded = JsonSerializer.Deserialize<AppConfig>(json, options);
                    if (loaded != null)
                        return loaded;

                    _loadFailed = true;
                    System.Diagnostics.Debug.WriteLine("配置文件内容为空或格式不合法");
                }
            }
            catch (Exception ex)
            {
                _loadFailed = true;
                System.Diagnostics.Debug.WriteLine($"加载配置失败: {ex.Message}");
            }
            return new AppConfig();
        }

        public void SaveConfig()
        {
            try
            {
                // 原文件解析失败时先留一份备份，避免默认值把用户配置直接覆盖掉
                if (_loadFailed && File.Exists(_configPath))
                {
                    var backup = _configPath + ".broken." + DateTime.Now.ToString("yyyyMMddHHmmss");
                    File.Copy(_configPath, backup, true);
                    System.Diagnostics.Debug.WriteLine($"原配置无法解析，已备份到 {backup}");
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_config, options);
                var tmp = _configPath + ".tmp";
                File.WriteAllText(tmp, json);
                File.Move(tmp, _configPath, true);
                _loadFailed = false;
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
