using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace StartMode.Services
{
    /// <summary>
    /// 本地 llama.cpp 推理服务：随启动器分发的 llama-server，按需启动/停止。
    /// </summary>
    public class LlamaService
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(2) };
        private readonly SemaphoreSlim _lock = new(1, 1);

        private readonly string _baseUrl;
        private readonly int _port;
        private readonly string _exePath;
        private readonly string _modelPath;

        private Process? _process;

        /// <summary>是否正在拉起模型进程（用于界面显示“启动中”）</summary>
        public bool IsStarting { get; private set; }

        public LlamaService()
        {
            var config = App.ConfigService.Config;
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            _baseUrl = string.IsNullOrWhiteSpace(config.LlamaBaseUrl)
                ? "http://127.0.0.1:8081"
                : config.LlamaBaseUrl.TrimEnd('/');
            _port = config.LlamaPort > 0 ? config.LlamaPort : 8081;
            _exePath = Path.IsPathRooted(config.LlamaServerPath)
                ? config.LlamaServerPath
                : Path.Combine(baseDir, config.LlamaServerPath);
            _modelPath = ResolveModelPath(config.LlamaModelPath, baseDir);
        }

        public string ModelPath => _modelPath;
        public int Port => _port;

        public async Task<bool> IsRunningAsync()
        {
            try
            {
                using var response = await Http.GetAsync(_baseUrl + "/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<(bool ok, string message)> StartAsync()
        {
            if (await IsRunningAsync())
                return (true, "AI 服务已在运行");

            await _lock.WaitAsync();
            try
            {
                if (await IsRunningAsync())
                    return (true, "AI 服务已在运行");

                if (!File.Exists(_exePath))
                    return (false, $"找不到 llama-server：{_exePath}");

                if (string.IsNullOrWhiteSpace(_modelPath) || !File.Exists(_modelPath))
                    return (false, "找不到模型文件，请在 appsettings.json 里配置 llamaModelPath");

                IsStarting = true;
                var startInfo = new ProcessStartInfo
                {
                    FileName = _exePath,
                    Arguments = $"-m \"{_modelPath}\" --host 127.0.0.1 --port {_port}",
                    WorkingDirectory = Path.GetDirectoryName(_exePath) ?? AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                _process = new Process { StartInfo = startInfo };
                _process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        Debug.WriteLine($"[llama] {e.Data}");
                };
                _process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        Debug.WriteLine($"[llama:err] {e.Data}");
                };

                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
                Debug.WriteLine($"[Llama] 启动中，模型：{_modelPath}");

                if (await WaitForReadyAsync(120))
                {
                    Debug.WriteLine("[Llama] 启动成功");
                    return (true, "AI 服务已启动");
                }

                Debug.WriteLine("[Llama] 启动超时，清理进程");
                StopInternal();
                return (false, "AI 服务启动超时（模型加载可能过慢）");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Llama] 启动失败: {ex.Message}");
                StopInternal();
                return (false, $"AI 服务启动失败：{ex.Message}");
            }
            finally
            {
                IsStarting = false;
                _lock.Release();
            }
        }

        public async Task<(bool ok, string message)> StopAsync()
        {
            await _lock.WaitAsync();
            try
            {
                var tracked = _process != null;
                StopInternal();

                // 即使不是本程序启动的，也按端口结束进程，确保端口释放
                var killedByPort = PortUtils.KillPort(_port);
                var released = PortUtils.WaitUntilClosed(_port, 8000) == 0;

                if (!released)
                    return (false, $"端口 {_port} 仍被占用，关闭失败");

                return (true, tracked || killedByPort ? "AI 服务已关闭" : "AI 服务本来就没在运行");
            }
            finally
            {
                _lock.Release();
            }
        }

        private void StopInternal()
        {
            var process = _process;
            _process = null;
            if (process == null)
                return;

            try
            {
                if (!process.HasExited)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/T /F /PID {process.Id}",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Llama] 结束进程失败: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }

        private async Task<bool> WaitForReadyAsync(int timeoutSeconds)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                if (_process?.HasExited == true)
                    return false;

                if (await IsRunningAsync())
                    return true;

                await Task.Delay(500);
            }
            return false;
        }

        private static string ResolveModelPath(string configured, string baseDir)
        {
            if (!string.IsNullOrWhiteSpace(configured))
                return Path.IsPathRooted(configured) ? configured : Path.Combine(baseDir, configured);

            // 未配置时优先使用随启动器分发的模型目录
            var bundledDir = Path.Combine(baseDir, "app", "models");
            if (Directory.Exists(bundledDir))
            {
                var model = Directory.GetFiles(bundledDir, "*.gguf").FirstOrDefault();
                if (model != null)
                    return model;
            }

            return string.Empty;
        }
    }
}
