using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NxDrawingPdfExporter.Contracts;

namespace NxDrawingPdfExporter.Core.Jobs
{
    /// <summary>
    /// 批处理致命错误的权威结果合并：保留最后一个可信持久快照中已完成的
    /// 文件结果，为未决条目补写明确的失败结果。绝不把已知结果替换为空数组，
    /// 也不发布任何未经校验的临时 PDF。
    /// </summary>
    public static class FatalResultMerger
    {
        public static JobResult Merge(JobRequest request, JobResult? lastSnapshot, string fatalMessage, DateTime endedUtc)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var snapshot = IsTrustedSnapshot(request, lastSnapshot) ? lastSnapshot : null;
            var files = new List<FileResult>(request.Items.Length);
            if (snapshot is not null)
            {
                files.AddRange(snapshot.Files);
            }

            // 快照按任务顺序只覆盖一个前缀；其余条目是本次致命错误的未决项。
            for (var index = files.Count; index < request.Items.Length; index++)
            {
                files.Add(new FileResult
                {
                    SourcePath = request.Items[index].SourcePath,
                    FinalOutputPath = request.Items[index].FinalOutputPath,
                    Status = FileResultStatus.Failed,
                    Message = "批处理在完成该文件前发生致命错误: " + Sanitize(fatalMessage)
                });
            }

            return new JobResult
            {
                RunId = request.RunId,
                StartedUtc = snapshot?.StartedUtc ?? endedUtc,
                EndedUtc = endedUtc,
                FatalError = Sanitize(fatalMessage),
                Files = files.ToArray()
            };
        }

        /// <summary>快照必须与请求同 RunId、同协议版本，且按任务顺序构成前缀，否则不可信。</summary>
        private static bool IsTrustedSnapshot(JobRequest request, JobResult? snapshot)
        {
            if (snapshot is null || snapshot.Files is null)
            {
                return false;
            }

            if (!string.Equals(snapshot.RunId, request.RunId, StringComparison.Ordinal)
                || !string.Equals(snapshot.ProtocolVersion, ProtocolVersion.Current, StringComparison.Ordinal)
                || snapshot.Files.Length > request.Items.Length)
            {
                return false;
            }

            for (var index = 0; index < snapshot.Files.Length; index++)
            {
                if (!string.Equals(snapshot.Files[index].SourcePath, request.Items[index].SourcePath, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static string Sanitize(string message)
        {
            return string.IsNullOrWhiteSpace(message) ? "未知错误" : Regex.Replace(message, "\\s+", " ").Trim();
        }
    }
}
