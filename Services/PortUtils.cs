using System.Diagnostics;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace StartMode.Services
{
    /// <summary>
    /// 端口探测与按端口结束进程，用于“关闭服务”时确保端口真正释放。
    /// </summary>
    internal static class PortUtils
    {
        public static bool IsPortOpen(int port)
        {
            if (port <= 0)
                return false;

            try
            {
                using var client = new TcpClient();
                var task = client.ConnectAsync("127.0.0.1", port);
                return task.Wait(500) && client.Connected;
            }
            catch
            {
                return false;
            }
        }

        public static bool KillPort(int port)
        {
            var pids = FindListeningPids(port);
            var killed = false;

            foreach (var pid in pids)
            {
                if (pid <= 0 || pid == Environment.ProcessId)
                    continue;

                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/T /F /PID {pid}",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    killed = true;
                    Debug.WriteLine($"[Port] 已结束占用 {port} 的进程 PID={pid}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Port] 结束 PID={pid} 失败: {ex.Message}");
                }
            }

            return killed;
        }

        public static int WaitUntilClosed(int port, int timeoutMs = 8000)
        {
            var deadline = Environment.TickCount64 + timeoutMs;
            while (Environment.TickCount64 < deadline)
            {
                if (!IsPortOpen(port))
                    return 0;
                Thread.Sleep(250);
            }
            return IsPortOpen(port) ? -1 : 0;
        }

        private static List<int> FindListeningPids(int port)
        {
            var result = new List<int>();

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-ano -p tcp",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                    return result;

                string? line;
                var pattern = new Regex($@":{port}\s+\S+\s+LISTENING\s+(\d+)\s*$", RegexOptions.IgnoreCase);
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    var match = pattern.Match(line.Trim());
                    if (match.Success)
                    {
                        var pid = int.Parse(match.Groups[1].Value);
                        if (!result.Contains(pid))
                            result.Add(pid);
                    }
                }

                process.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Port] 查询 {port} 占用失败: {ex.Message}");
            }

            return result;
        }
    }
}
