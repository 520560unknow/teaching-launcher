using System.Diagnostics;
using System.IO;
using System.Net.Http;
using StartMode.Models;

namespace StartMode.Services
{
    public class JavaAppService : ManagedProcessService, IJavaAppService
    {
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(3) };
        private readonly string _healthUrl;
        private readonly string _jarPath;
        private readonly string _configJavaPath;

        private string? _resolvedJavaExe;

        public JavaAppService(AppConfig config)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;

            _healthUrl = config.ServerHealthUrl;
            _jarPath = Path.Combine(baseDir, config.SpringBootJarPath);
            _configJavaPath = Path.Combine(baseDir, config.JavaExePath);
        }

        protected override string ServiceTag => "[JavaApp]";
        protected override string OutputTag => "[java]";
        protected override string ErrorTag => "[java:err]";
        protected override string StartingLog => "[JavaApp] 正在启动 Java 应用...";
        protected override string SuccessLog => "[JavaApp] Java 应用启动成功";
        protected override string TimeoutLog => "[JavaApp] Java 应用启动超时，清理进程";
        protected override string TimeoutMessage => "网页服务启动超时";
        protected override int TimeoutSeconds => 60;
        protected override int FastPollWindowSeconds => 10;
        protected override int FastPollIntervalMs => 300;
        protected override int SlowPollIntervalMs => 1000;

        protected override Task<bool> ProbeReadyAsync() => IsServerHealthyAsync();

        protected override string FormatFailure(Exception ex) => string.Empty;

        protected override void LogKilling(int pid) => Debug.WriteLine($"[JavaApp] 杀进程树 PID={pid}");

        protected override void LogKillFailed(Exception ex) => Debug.WriteLine($"[JavaApp] 停止失败: {ex.Message}");

        public async Task<bool> IsServerHealthyAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(_healthUrl);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> EnsureStartedAsync()
        {
            if (await IsServerHealthyAsync())
                return true;

            return await WithLockAsync(async () =>
            {
                if (await IsServerHealthyAsync())
                    return true;

                return await StartJavaAppAsync();
            });
        }

        private async Task<bool> StartJavaAppAsync()
        {
            var (ok, _) = await LaunchAsync();
            return ok;
        }

        protected override Task<string?> ValidateAsync()
        {
            if (!File.Exists(_jarPath))
            {
                Debug.WriteLine($"[JavaApp] JAR 文件不存在: {_jarPath}");
                return Task.FromResult<string?>(string.Empty);
            }

            _resolvedJavaExe = FindJavaExecutable();
            if (_resolvedJavaExe == null)
            {
                Debug.WriteLine("[JavaApp] 找不到 Java 可执行文件");
                return Task.FromResult<string?>(string.Empty);
            }

            return Task.FromResult<string?>(null);
        }

        protected override ProcessStartInfo BuildStartInfo() => new()
        {
            FileName = _resolvedJavaExe!,
            Arguments = $"-jar \"{_jarPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(_jarPath)
        };

        public Task<bool> StartAsync() => EnsureStartedAsync();

        public async Task<(bool ok, string message)> StopAsync()
        {
            return await WithLockAsync(async () =>
            {
                var port = GetPort(_healthUrl);
                var killed = false;

                if (TrackedProcess != null)
                {
                    StopTrackedProcess();
                    killed = true;
                }

                // 外部启动的 java 进程也要能关闭：按端口找到 PID 并结束
                if (port > 0 && (await IsServerHealthyAsync() || PortUtils.IsPortOpen(port)))
                {
                    killed = PortUtils.KillPort(port) || killed;
                    if (PortUtils.WaitUntilClosed(port, 8000) != 0)
                        return (false, $"端口 {port} 仍被占用，关闭失败");
                }

                return (true, killed ? "网页服务已关闭" : "网页服务本来就没在运行");
            });
        }

        private static int GetPort(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Port : 0;
        }

        private string? FindJavaExecutable()
        {
            // 1. 配置的路径
            if (File.Exists(_configJavaPath))
                return _configJavaPath;

            // 2. 默认 fallback
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var fallback = Path.Combine(baseDir, "app", "jre8", "bin", "java.exe");
            if (File.Exists(fallback))
                return fallback;

            // 3. 系统 PATH 中的 java
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                var p = Process.Start(psi);
                if (p != null)
                {
                    p.WaitForExit(3000);
                    return "java";
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// 退出兜底：结束自己跟踪的进程树；若端口仍被 java 进程占用（例如上次异常退出留下的），
        /// 也一并清理，避免下次启动端口被占。
        /// </summary>
        public void StopJavaApp()
        {
            StopTrackedProcess();

            var port = GetPort(_healthUrl);
            if (port > 0 && PortUtils.IsHeldBy(port, "java", "javaw"))
            {
                Debug.WriteLine($"[JavaApp] 退出时清理占用 {port} 的残留 java 进程");
                PortUtils.KillPort(port);
            }
        }
    }
}
