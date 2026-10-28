using System.IO;
using System.Text.Json;
using StudentMessageBall.Models;

namespace StudentMessageBall.Services;

/// <summary>
/// 配置管理服务 - 读取和管理 appsettings.json
/// </summary>
public class ConfigService
{
    private static readonly Lazy<ConfigService> _instance = new(() => new ConfigService());
    public static ConfigService Instance => _instance.Value;

    private readonly string _configPath;
    private AppConfig _config;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AppConfig Config => _config;

    private ConfigService()
    {
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        _config = LoadConfig();
    }

    private AppConfig LoadConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions);
                if (config != null) return config;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载配置失败: {ex.Message}");
        }

        // 配置不存在或加载失败时生成默认配置
        var defaultConfig = new AppConfig();
        SaveConfig(defaultConfig);
        return defaultConfig;
    }

    public void SaveConfig(AppConfig? config = null)
    {
        try
        {
            var toSave = config ?? _config;
            var json = JsonSerializer.Serialize(toSave, _jsonOptions);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存配置失败: {ex.Message}");
        }
    }

    public void Save()
    {
        SaveConfig(_config);
    }
}