using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace NxDrawingPdfExporter.App.Runtime
{
    public sealed class WorkerLaunchRequest
    {
        public string NxLauncherPath { get; set; } = "";

        public string WorkerExePath { get; set; } = "";

        public string JobPath { get; set; } = "";

        public string RunDirectory { get; set; } = "";

        public int TimeoutSeconds { get; set; } = 3600;
    }

    public sealed class WorkerLaunchResult
    {
        public int ExitCode { get; set; }

        public bool TimedOut { get; set; }

        public string StandardOutputPath { get; set; } = "";

        public string StandardErrorPath { get; set; } = "";
    }

    /// <summary>Worker 进程启动抽象，测试可替换。</summary>
    public interface IWorkerProcessLauncher
    {
        WorkerLaunchResult Launch(WorkerLaunchRequest request);
    }

    /// <summary>
    /// 通过门槛验证过的 run_managed.exe 命令启动 Worker，异步捕获
    /// stdout/stderr 到运行目录，等待退出（超时只终止自己启动的进程）。
    /// 退出码不是权威结果；result.json 才是。
    /// </summary>
    public sealed class WorkerProcessLauncher : IWorkerProcessLauncher
    {
        public WorkerLaunchResult Launch(WorkerLaunchRequest request)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            foreach (var path in new[] { request.NxLauncherPath, request.WorkerExePath })
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    throw new InvalidOperationException("NX 启动器或 Worker 程序不存在。");
                }
            }

            var stdoutPath = Path.Combine(request.RunDirectory, "worker-stdout.txt");
            var stderrPath = Path.Combine(request.RunDirectory, "worker-stderr.txt");

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = request.NxLauncherPath,
                Arguments = Quote(request.WorkerExePath) + " --run-job " + Quote(request.JobPath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = request.RunDirectory
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("无法启动 NX Worker 进程。");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            var timedOut = false;
            if (!process.WaitForExit(request.TimeoutSeconds * 1000))
            {
                timedOut = true;
                try
                {
                    process.Kill();
                    process.WaitForExit(120000);
                }
                catch (Exception error)
                {
                    stderrTask.Wait(5000);
                    File.AppendAllText(stderrPath, "终止超时 Worker 进程失败: " + error.Message + Environment.NewLine);
                }
            }

            File.WriteAllText(stdoutPath, stdoutTask.GetAwaiter().GetResult());
            File.WriteAllText(stderrPath, stderrTask.GetAwaiter().GetResult());

            return new WorkerLaunchResult
            {
                ExitCode = timedOut ? -1 : process.ExitCode,
                TimedOut = timedOut,
                StandardOutputPath = stdoutPath,
                StandardErrorPath = stderrPath
            };
        }

        private static string Quote(string path) => "\"" + path + "\"";
    }
}
