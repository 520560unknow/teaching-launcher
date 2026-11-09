using System.Diagnostics;
using System.Threading;

namespace StartMode.Services
{
    /// <summary>
    /// 外部进程服务的公共骨架：启动前校验 → 起进程并重定向输出 → 轮询就绪 → 超时/异常清理，
    /// 以及停止时的杀进程树。子类只负责可执行文件、启动参数、就绪探测与超时等差异。
    /// 锁语义保持为「每个服务实例一把信号量」，由 <see cref="WithLockAsync{T}"/> 统一持有。
    /// </summary>
    public abstract class ManagedProcessService
    {
        private readonly SemaphoreSlim _gate = new(1, 1);

        private Process? _process;

        /// <summary>是否正在拉起进程（用于界面显示“启动中”）</summary>
        public bool IsStarting { get; private set; }

        protected Process? TrackedProcess => _process;

        // ---- 子类差异：日志标签 ----

        protected abstract string ServiceTag { get; }

        protected abstract string OutputTag { get; }

        protected abstract string ErrorTag { get; }

        // ---- 子类差异：启动模板钩子 ----

        /// <summary>启动前校验；返回 null 表示通过，否则返回失败信息。</summary>
        protected abstract Task<string?> ValidateAsync();

        /// <summary>构造子进程启动参数。</summary>
        protected abstract ProcessStartInfo BuildStartInfo();

        /// <summary>就绪等待超时时返回给调用方的提示。</summary>
        protected abstract string TimeoutMessage { get; }

        /// <summary>就绪等待的最长秒数。</summary>
        protected abstract int TimeoutSeconds { get; }

        /// <summary>启动异常时组装失败信息。</summary>
        protected virtual string FormatFailure(Exception ex) => $"{ServiceTag} 启动失败：{ex.Message}";

        protected virtual string StartingLog => $"{ServiceTag} 正在启动...";

        protected virtual string SuccessLog => $"{ServiceTag} 启动成功";

        protected virtual string TimeoutLog => $"{ServiceTag} 启动超时，清理进程";

        // ---- 子类差异：就绪轮询 ----

        /// <summary>就绪探测（HTTP 健康检查 / 端口检查等）。</summary>
        protected abstract Task<bool> ProbeReadyAsync();

        /// <summary>轮询加快的窗口期（秒）；期间使用 <see cref="FastPollIntervalMs"/>。</summary>
        protected virtual int FastPollWindowSeconds => 0;

        protected virtual int FastPollIntervalMs => 500;

        protected virtual int SlowPollIntervalMs => 500;

        /// <summary>进程提前退出时是否立即判定启动失败。</summary>
        protected virtual bool AbortIfProcessExited => false;

        // ---- 供子类复用的锁 ----

        protected async Task<T> WithLockAsync<T>(Func<Task<T>> body)
        {
            await _gate.WaitAsync();
            try
            {
                return await body();
            }
            finally
            {
                _gate.Release();
            }
        }

        protected async Task<T> WithLockAsync<T>(Func<T> body)
        {
            await _gate.WaitAsync();
            try
            {
                return body();
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// 启动模板（调用方需已持有 <see cref="WithLockAsync{T}"/> 的锁）：
        /// 校验 → 起进程并重定向输出 → 轮询就绪 → 超时/异常清理。
        /// 子类只实现差异钩子，流程本身不重复。
        /// </summary>
        protected async Task<(bool ok, string message)> LaunchAsync()
        {
            var validationError = await ValidateAsync();
            if (validationError != null)
                return (false, validationError);

            try
            {
                IsStarting = true;

                var process = new Process { StartInfo = BuildStartInfo() };
                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        Debug.WriteLine($"{OutputTag} {e.Data}");
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        Debug.WriteLine($"{ErrorTag} {e.Data}");
                };

                _process = process;
                if (!process.Start())
                    throw new InvalidOperationException("进程未能启动");
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                Debug.WriteLine(StartingLog);
                if (await WaitForReadyAsync(TimeoutSeconds))
                {
                    Debug.WriteLine(SuccessLog);
                    return (true, string.Empty);
                }

                Debug.WriteLine(TimeoutLog);
                StopTrackedProcess();
                return (false, TimeoutMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{ServiceTag} 启动失败: {ex.Message}");
                StopTrackedProcess();
                return (false, FormatFailure(ex));
            }
            finally
            {
                IsStarting = false;
            }
        }

        private async Task<bool> WaitForReadyAsync(int timeoutSeconds)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            var fastUntil = DateTime.UtcNow.AddSeconds(FastPollWindowSeconds);

            while (DateTime.UtcNow < deadline)
            {
                if (AbortIfProcessExited && _process?.HasExited == true)
                    return false;

                if (await ProbeReadyAsync())
                    return true;

                var delay = DateTime.UtcNow < fastUntil ? FastPollIntervalMs : SlowPollIntervalMs;
                await Task.Delay(delay);
            }

            return false;
        }

        /// <summary>杀掉当前跟踪的进程树并释放引用，同步返回，不等待端口释放。</summary>
        protected void StopTrackedProcess()
        {
            var process = _process;
            _process = null;
            if (process == null)
                return;

            try
            {
                if (!process.HasExited)
                {
                    LogKilling(process.Id);
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
                LogKillFailed(ex);
            }
            finally
            {
                process.Dispose();
            }
        }

        protected virtual void LogKilling(int pid) { }

        protected virtual void LogKillFailed(Exception ex) { }
    }
}
