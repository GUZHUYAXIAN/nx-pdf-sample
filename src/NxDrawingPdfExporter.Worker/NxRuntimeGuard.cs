using System;
using System.Diagnostics;
using System.IO;
using NXOpen;
using NXOpen.UF;

namespace NxDrawingPdfExporter.Worker
{
    internal static class NxRuntimeGuard
    {
        public static void Verify()
        {
#if NX2506
            const string label = "NX 2506";
            const string bin = "NXBIN";
            string[] expected = { "2506.0.0.0", "2506.1700.0.0", "2506.0.0.0" };
#else
            const string label = "NX 10";
            const string bin = "UGII";
            string[] expected = { "10.0.0.24", "10.0.0.23", "4.0.0.0" };
#endif
            string root = Environment.GetEnvironmentVariable("UGII_BASE_DIR") ?? "";
            if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("未绑定 NX 根目录，已停止。");
            string managed = Path.GetFullPath(Path.Combine(root, bin, "managed"));
            var assemblies = new[] { typeof(Session).Assembly, typeof(UFSession).Assembly, typeof(Session).BaseType!.Assembly };
            for (int i = 0; i < assemblies.Length; i++)
            {
                string location = Path.GetFullPath(assemblies[i].Location);
                if (!string.Equals(Path.GetDirectoryName(location), managed, StringComparison.OrdinalIgnoreCase)
                    || FileVersionInfo.GetVersionInfo(location).FileVersion != expected[i])
                    throw new InvalidOperationException(label + " 加载了来源或版本不匹配的 NX 组件，已停止；请重新检测安装。");
            }
            if (Session.GetSession() == null || UFSession.GetUFSession() == null)
                throw new InvalidOperationException(label + " 无法取得真实 Session/UFSession，已停止。");
            Console.WriteLine("NX_RUNTIME_VERIFIED " + label + " Session=True UFSession=True AssemblyOrigins=True Versions=" + string.Join("/", expected));
        }
    }
}
