using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;

namespace StartMode.Services
{
    public class JavaAppService
    {
        private readonly SemaphoreSlim _startLock = new(1, 1);
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(3) };
        private readonly string _healthUrl;
        private readonly string _jarPath;
        private readonly string _configJavaPath;

        private Process? _javaProcess;
        private bool _startedByUs;

        /// <summary>是否正在拉起 Java 进程（用于界面显示“启动中”）</summary>
        public bool IsStarting { get; private set; }

        public JavaAppService()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var config = App.ConfigService.Config;

            _healthUrl = config.ServerHealthUrl;
            _jarPath = Path.Combine(baseDir, config.SpringBootJarPath);
            _configJavaPath = Path.Combine(baseDir, config.JavaExePath);
        }

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

            await _startLock.WaitAsync();
            try
            {
                if (await IsServerHealthyAsync())
                    return true;

                return await StartJavaAppAsync();
            }
            finally
            {
                _startLock.Release();
            }
        }

        private async Task<bool> StartJavaAppAsync()
        {
            if (!File.Exists(_jarPath))
            {
                Debug.WriteLine($"[JavaApp] JAR 文件不存在: {_jarPath}");
                return false;
            }

            string? javaExe = FindJavaExecutable();
            if (javaExe == null)
            {
                Debug.WriteLine("[JavaApp] 找不到 Java 可执行文件");
                return false;
            }

            try
            {
                IsStarting = true;
                var startInfo = new ProcessStartInfo
                {
                    FileName = javaExe,
                    Arguments = $"-jar \"{_jarPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(_jarPath)
                };

                _javaProcess = new Process { StartInfo = startInfo };
                _javaProcess.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        Debug.WriteLine($"[java] {e.Data}");
                };
                _javaProcess.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        Debug.WriteLine($"[java:err] {e.Data}");
                };

                _javaProcess.Start();
                _javaProcess.BeginOutputReadLine();
                _javaProcess.BeginErrorReadLine();
                _startedByUs = true;

                Debug.WriteLine("[JavaApp] 正在启动 Java 应用...");
                var ready = await WaitForHealthAsync(60);

                if (ready)
                {
                    Debug.WriteLine("[JavaApp] Java 应用启动成功");
                    return true;
                }
                else
                {
                    Debug.WriteLine("[JavaApp] Java 应用启动超时，清理进程");
                    KillSpawnedProcess();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[JavaApp] 启动失败: {ex.Message}");
                KillSpawnedProcess();
                return false;
            }
            finally
            {
                IsStarting = false;
            }
        }

        public Task<bool> StartAsync() => EnsureStartedAsync();

        public async Task<(bool ok, string message)> StopAsync()
        {
            await _startLock.WaitAsync();
            try
            {
                var port = GetPort(_healthUrl);
                var killed = false;

                if (_javaProcess != null)
                {
                    KillSpawnedProcess();
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
            }
            finally
            {
                _startLock.Release();
            }
        }

        private static int GetPort(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Port : 0;
        }

        private async Task<bool> WaitForHealthAsync(int timeoutSeconds)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            var fastUntil = DateTime.UtcNow.AddSeconds(10);

            while (DateTime.UtcNow < deadline)
            {
                if (await IsServerHealthyAsync())
                    return true;

                var delay = DateTime.UtcNow < fastUntil ? 300 : 1000;
                await Task.Delay(delay);
            }

            return false;
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

        public void StopJavaApp()
        {
            if (!_startedByUs)
                return;

            KillSpawnedProcess();
        }

        private void KillSpawnedProcess()
        {
            if (_javaProcess == null)
                return;

            try
            {
                if (!_javaProcess.HasExited)
                {
                    var pid = _javaProcess.Id;
                    Debug.WriteLine($"[JavaApp] 杀进程树 PID={pid}");

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/T /F /PID {pid}",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[JavaApp] 停止失败: {ex.Message}");
            }
            finally
            {
                _javaProcess.Dispose();
                _javaProcess = null;
                _startedByUs = false;
            }
        }
    }
}
