using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace NxDrawingPdfExporter.Contracts
{
    /// <summary>
    /// Versioned JSON reader/writer for the cross-process job/result protocol.
    /// Uses <see cref="DataContractJsonSerializer"/> so netstandard2.0 consumers and the
    /// net48 Worker share one implementation without an extra JSON dependency.
    /// Writes are atomic: serialize to a same-directory unique temp file, flush, then move/replace.
    /// </summary>
    public static class JobJsonSerializer
    {
        public static string Serialize<T>(T value)
        {
            if (value is null)
            {
                throw new ProtocolException("无法序列化空协议对象。");
            }

            var serializer = new DataContractJsonSerializer(typeof(T));
            using var stream = new MemoryStream();
            serializer.WriteObject(stream, value);
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        public static T Deserialize<T>(string json)
        {
            object raw;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                raw = serializer.ReadObject(stream)!;
            }
            catch (Exception error) when (error is SerializationException || error is FormatException || error is XmlException)
            {
                throw new ProtocolException($"协议 JSON 解析失败: {Sanitize(error.Message)}", error);
            }

            if (raw is null)
            {
                throw new ProtocolException("协议 JSON 根对象不能为空。");
            }

            if (raw is JobRequest request)
            {
                Validate(request);
            }
            else if (raw is JobResult result)
            {
                Validate(result);
            }

            return (T)raw;
        }

        public static void WriteToFile<T>(T value, string path)
        {
            if (value is null)
            {
                throw new ProtocolException("无法写入空协议对象。");
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ProtocolException("协议文件路径不能为空。");
            }

            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new ProtocolException("协议文件路径缺少有效目录。");
            }

            Directory.CreateDirectory(directory);

            var tempPath = Path.Combine(
                directory,
                Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    serializer.WriteObject(stream, value);
                    stream.Flush();
                }

                if (File.Exists(fullPath))
                {
                    File.Replace(tempPath, fullPath, null);
                }
                else
                {
                    File.Move(tempPath, fullPath);
                }
            }
            catch (Exception error)
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch (Exception cleanupError)
                {
                    throw new IOException($"协议临时文件清理失败: {Sanitize(cleanupError.Message)}", error);
                }

                if (error is ProtocolException)
                {
                    throw;
                }

                throw new ProtocolException($"协议文件写入失败: {Sanitize(error.Message)}", error);
            }
        }

        public static T ReadFromFile<T>(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ProtocolException("协议文件路径不能为空。");
            }

            string json;
            try
            {
                json = File.ReadAllText(Path.GetFullPath(path), Encoding.UTF8);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                throw new ProtocolException($"协议文件读取失败: {Sanitize(error.Message)}", error);
            }

            return Deserialize<T>(json);
        }

        private static void Validate(JobRequest request)
        {
            if (request is null)
            {
                throw new ProtocolException("任务请求为空。");
            }

            ValidateVersion(request.ProtocolVersion);
            RequireText(request.RunId, "RunId");
            RequireAbsolute(request.ResultPath, "ResultPath");
            RequireAbsolute(request.CancellationFlagPath, "CancellationFlagPath");

            if (!Enum.IsDefined(typeof(OutputMode), request.OutputMode))
            {
                throw new ProtocolException($"未知的输出模式: {request.OutputMode}");
            }

            if (!Enum.IsDefined(typeof(ExistingPdfPolicy), request.ExistingPdfPolicy))
            {
                throw new ProtocolException($"未知的已有 PDF 策略: {request.ExistingPdfPolicy}");
            }

            if (request.Items is null)
            {
                throw new ProtocolException("Items 不能为 null。");
            }

            var tempPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var finalPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < request.Items.Length; i++)
            {
                var item = request.Items[i];
                if (item is null)
                {
                    throw new ProtocolException($"Items[{i}] 为 null。");
                }

                RequireAbsolute(item.SourcePath, $"Items[{i}].SourcePath");
                RequireAbsolute(item.FinalOutputPath, $"Items[{i}].FinalOutputPath");
                RequireAbsolute(item.WorkerTempOutputPath, $"Items[{i}].WorkerTempOutputPath");

                var temporary = Path.GetFullPath(item.WorkerTempOutputPath);
                var final = Path.GetFullPath(item.FinalOutputPath);
                if (!string.Equals(Path.GetExtension(final), ".pdf", StringComparison.OrdinalIgnoreCase)
                    || !temporary.EndsWith(".tmp.pdf", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(Path.GetDirectoryName(temporary), Path.GetDirectoryName(final), StringComparison.OrdinalIgnoreCase)
                    || !tempPaths.Add(temporary) || !finalPaths.Add(final))
                {
                    throw new ProtocolException("任务 PDF 路径必须唯一，临时 PDF 与正式 PDF 必须同目录。");
                }
            }

            if (tempPaths.Overlaps(finalPaths))
                throw new ProtocolException("临时路径与正式目标冲突。");

            if (request.OutputMode == OutputMode.UnifiedDirectory)
            {
                RequireAbsolute(request.UnifiedOutputDirectory ?? string.Empty, "UnifiedOutputDirectory");
            }
        }

        private static void Validate(JobResult result)
        {
            if (result is null)
            {
                throw new ProtocolException("任务结果为空。");
            }

            ValidateVersion(result.ProtocolVersion);
            RequireText(result.RunId, "RunId");

            if (result.Files is null)
            {
                throw new ProtocolException("Files 不能为 null。");
            }

            foreach (var file in result.Files)
            {
                if (file is null)
                {
                    throw new ProtocolException("Files 包含 null 项。");
                }

                RequireText(file.ReasonCode, "ReasonCode");
                if (!Enum.IsDefined(typeof(FileResultStatus), file.Status)
                    || file.ReasonArguments is null || file.TechnicalDetails is null)
                {
                    throw new ProtocolException("文件结果状态或原因字段无效。");
                }
            }
        }

        private static void ValidateVersion(string version)
        {
            if (!string.Equals(version, ProtocolVersion.Current, StringComparison.Ordinal))
            {
                throw new ProtocolException($"不支持的协议版本: '{version}'（当前版本 {ProtocolVersion.Current}）。");
            }
        }

        private static void RequireText(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ProtocolException($"必填字段 {field} 不能为空。");
            }
        }

        private static void RequireAbsolute(string value, string field)
        {
            RequireText(value, field);
            try
            {
                var root = Path.GetPathRoot(value);
                if (string.IsNullOrEmpty(root) || root.Length == 1 || (root.Length == 2 && root[1] == ':'))
                    throw new ProtocolException($"必填字段 {field} 必须是绝对路径。");
                Path.GetFullPath(value);
            }
            catch (Exception error) when (error is ArgumentException || error is IOException || error is NotSupportedException)
            {
                throw new ProtocolException($"必填字段 {field} 包含无效路径。", error);
            }
        }

        private static string Sanitize(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return "未知错误";
            }

            return Regex.Replace(message, "\\s+", " ").Trim();
        }
    }
}
