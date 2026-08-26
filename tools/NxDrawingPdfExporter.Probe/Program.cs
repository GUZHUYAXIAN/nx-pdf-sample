using System;
using System.IO;
using System.Text;
using NXOpen;
using NXOpen.UF;

namespace NxDrawingPdfExporter.Probe
{
    /// <summary>
    /// Gate 1 probe: proves that the verified local NX 10 run_managed.exe can load this
    /// net48 x64 assembly and hand back a REAL Session and UFSession.
    /// It never opens, imports, or saves any PRT. One UTF-8 JSON report is written to
    /// the path supplied as the first argument.
    /// Exit codes: 0 = both sessions obtained; 20 = session(s) null; 21 = exception.
    /// </summary>
    internal static class Program
    {
        private const int ExitOk = 0;
        private const int ExitNoSession = 20;
        private const int ExitFailure = 21;

        [STAThread]
        private static int Main(string[] args)
        {
            var reportPath = args.Length > 0 ? args[0] : null;
            var report = new ProbeReport
            {
                UtcTimestamp = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                ProcessBitness = Environment.Is64BitProcess ? "x64" : "x86",
                RuntimeVersion = Environment.Version.ToString(),
                CurrentDirectory = Environment.CurrentDirectory,
                ArgumentEcho = args,
                NxRootEnvironment = Environment.GetEnvironmentVariable("UGII_ROOT_DIR") ?? ""
            };

            try
            {
                var session = Session.GetSession();
                var ufSession = UFSession.GetUFSession();

                report.NxSessionAvailable = session != null;
                report.UfSessionAvailable = ufSession != null;

                if (!report.NxSessionAvailable || !report.UfSessionAvailable)
                {
                    return Finish(reportPath, report, ExitNoSession);
                }

                return Finish(reportPath, report, ExitOk);
            }
            catch (Exception error)
            {
                report.Error = Sanitize(error.GetType().FullName + ": " + error.Message);
                TryWrite(reportPath, report);
                return ExitFailure;
            }
        }
        private static int Finish(string? reportPath, ProbeReport report, int exitCode)
        {
            report.ExitCode = exitCode;
            TryWrite(reportPath, report);
            return exitCode;
        }

        private static void TryWrite(string? reportPath, ProbeReport report)
        {
            if (string.IsNullOrWhiteSpace(reportPath))
            {
                Console.Out.Write(report.ToJson());
                Console.Out.Flush();
                return;
            }

            var fullPath = Path.GetFullPath(reportPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, report.ToJson(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static string Sanitize(string value) => (value ?? "").Replace('\r', ' ').Replace('\n', ' ');
    }
}
