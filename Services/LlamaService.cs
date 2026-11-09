using System.Diagnostics;
using System.IO;
using System.Net.Http;
using StartMode.Models;

namespace StartMode.Services
{
    /// <summary>
    /// 本地 llama.cpp 推理服务：随启动器分发的 llama-server，按需启动/停止。
    /// </summary>
    public class LlamaService : ManagedProcessService, ILlamaService
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(2) };

        private readonly string _baseUrl;
        private readonly int _port;
        private readonly string _exePath;
        private readonly string _modelPath;

        public LlamaService(AppConfig config)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 端口以 llamaPort 为准（启动 llama-server 时用的就是它），
            // 地址只取配置里的协议与主机，避免两处配置打架导致"起来了但探测不到"。
            _port = config.LlamaPort > 0 ? config.LlamaPort : 8081;

            var scheme = "http";
            var host = "127.0.0.1";
            if (!string.IsNullOrWhiteSpace(config.LlamaBaseUrl)
                && Uri.TryCreate(config.LlamaBaseUrl.Trim(), UriKind.Absolute, out var configured))
            {
                scheme = configured.Scheme;
                host = configured.Host;
                if (!configured.IsDefaultPort && configured.Port != _port)
                {
                    Debug.WriteLine($"[Llama] 配置地址端口 {configured.Port} 与 llamaPort {_port} 不一致，以 llamaPort 为准");
                }
            }

            _baseUrl = $"{scheme}://{host}:{_port}";
            _exePath = Path.IsPathRooted(config.LlamaServerPath)
                ? config.LlamaServerPath
                : Path.Combine(baseDir, config.LlamaServerPath);
            _modelPath = ResolveModelPath(config.LlamaModelPath, baseDir);
        }

        public string ModelPath => _modelPath;
        public int Port => _port;

        protected override string ServiceTag => "[Llama]";
        protected override string OutputTag => "[llama]";
        protected override string ErrorTag => "[llama:err]";
        protected override string StartingLog => $"[Llama] 启动中，模型：{_modelPath}";
        protected override string TimeoutMessage => "AI 服务启动超时（模型加载可能过慢）";
        protected override int TimeoutSeconds => 120;
        protected override bool AbortIfProcessExited => true;

        protected override Task<bool> ProbeReadyAsync() => IsRunningAsync();

        protected override string FormatFailure(Exception ex) => $"AI 服务启动失败：{ex.Message}";

        protected override void LogKillFailed(Exception ex) => Debug.WriteLine($"[Llama] 结束进程失败: {ex.Message}");

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

            return await WithLockAsync(async () =>
            {
                if (await IsRunningAsync())
                    return (true, "AI 服务已在运行");

                var (ok, message) = await LaunchAsync();

                return ok ? (true, "AI 服务已启动") : (ok, message);
            });
        }

        protected override Task<string?> ValidateAsync()
        {
            if (!File.Exists(_exePath))
                return Task.FromResult<string?>($"找不到 llama-server：{_exePath}");

            if (string.IsNullOrWhiteSpace(_modelPath) || !File.Exists(_modelPath))
                return Task.FromResult<string?>("找不到模型文件，请在 appsettings.json 里配置 llamaModelPath");

            return Task.FromResult<string?>(null);
        }

        protected override ProcessStartInfo BuildStartInfo() => new()
        {
            FileName = _exePath,
            Arguments = $"-m \"{_modelPath}\" --host 127.0.0.1 --port {_port}",
            WorkingDirectory = Path.GetDirectoryName(_exePath) ?? AppDomain.CurrentDomain.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        public async Task<(bool ok, string message)> StopAsync()
        {
            return await WithLockAsync<(bool ok, string message)>(() =>
            {
                var tracked = TrackedProcess != null;
                StopTrackedProcess();

                // 即使不是本程序启动的，也按端口结束进程，确保端口释放
                var killedByPort = PortUtils.KillPort(_port);
                var released = PortUtils.WaitUntilClosed(_port, 8000) == 0;

                if (!released)
                    return (false, $"端口 {_port} 仍被占用，关闭失败");

                return (true, tracked || killedByPort ? "AI 服务已关闭" : "AI 服务本来就没在运行");
            });
        }

        /// <summary>退出时使用：不等端口释放，直接结束进程并清理端口占用。</summary>
        public void StopImmediately()
        {
            StopTrackedProcess();
            try { PortUtils.KillPort(_port); }
            catch (Exception ex) { Debug.WriteLine($"[Llama] 强制结束失败: {ex.Message}"); }
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
