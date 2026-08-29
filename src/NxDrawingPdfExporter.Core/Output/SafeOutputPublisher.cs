using System;
using System.IO;
using NxDrawingPdfExporter.Core.Pdf;

namespace NxDrawingPdfExporter.Core.Output
{
    public enum PublicationOutcomeStatus
    {
        /// <summary>原先没有目标文件，校验通过的新 PDF 已发布。</summary>
        Published,

        /// <summary>旧目标文件已被校验通过的新 PDF 安全覆盖。</summary>
        Replaced,

        /// <summary>临时 PDF 未通过校验，未发布任何内容。</summary>
        ValidationFailed,

        /// <summary>发布或替换失败；在覆盖场景中已尽力恢复原有文件。</summary>
        PublishFailed
    }

    public sealed class PublicationRequest
    {
        public string TempPdfPath { get; set; } = "";

        public string FinalOutputPath { get; set; } = "";

        public int ExpectedPageCount { get; set; }
    }

    public sealed class PublicationOutcome
    {
        public PublicationOutcomeStatus Status { get; set; }

        /// <summary>面向用户的中文结果说明，不含堆栈信息。</summary>
        public string Message { get; set; } = "";

        /// <summary>失败时原目标文件是否保持完好。</summary>
        public bool OriginalPreserved { get; set; }
    }

    /// <summary>
    /// 事务性发布：先完整校验 Worker 输出的临时 PDF，再原子发布或安全覆盖；
    /// 任何失败都不会把残缺 PDF 留作正式输出。
    /// </summary>
    public sealed class SafeOutputPublisher
    {
        private readonly IPdfInspector inspector;
        private readonly IFileReplacer replacer;

        public SafeOutputPublisher(IPdfInspector inspector, IFileReplacer replacer)
        {
            this.inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
            this.replacer = replacer ?? throw new ArgumentNullException(nameof(replacer));
        }

        public PublicationOutcome Publish(PublicationRequest request)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.TempPdfPath))
            {
                throw new ArgumentException("临时 PDF 路径不能为空。", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.FinalOutputPath))
            {
                throw new ArgumentException("目标 PDF 路径不能为空。", nameof(request));
            }

            if (request.ExpectedPageCount < 1)
            {
                throw new ArgumentException("预期页数必须至少为 1。", nameof(request));
            }

            return PublishCore(request);
        }

        private PublicationOutcome PublishCore(PublicationRequest request)
        {
            var hadExistingTarget = File.Exists(request.FinalOutputPath);

            var validationFailure = ValidateTempPdf(request);
            if (validationFailure is not null)
            {
                DeleteOwnedFileIfExists(request.TempPdfPath);
                return new PublicationOutcome
                {
                    Status = PublicationOutcomeStatus.ValidationFailed,
                    Message = validationFailure,
                    OriginalPreserved = hadExistingTarget
                };
            }

            return hadExistingTarget ? ReplaceExisting(request) : PublishNew(request);
        }

        private string? ValidateTempPdf(PublicationRequest request)
        {
            if (!File.Exists(request.TempPdfPath))
            {
                return "临时 PDF 不存在。";
            }

            if (new FileInfo(request.TempPdfPath).Length == 0)
            {
                return "临时 PDF 是空文件。";
            }

            PdfInspection inspection;
            try
            {
                inspection = inspector.Inspect(request.TempPdfPath);
            }
            catch (Exception ex)
            {
                return "PDF 校验失败: " + ex.Message;
            }

            if (!inspection.HasPdfHeader)
            {
                return "临时 PDF 不是有效的 PDF 文件（缺少 PDF 文件头）。";
            }

            if (inspection.PageCount != request.ExpectedPageCount)
            {
                return $"PDF 页数 {inspection.PageCount} 与有效图纸页数 {request.ExpectedPageCount} 不一致。";
            }

            return null;
        }

        private PublicationOutcome PublishNew(PublicationRequest request)
        {
            try
            {
                replacer.MoveIntoPlace(request.TempPdfPath, request.FinalOutputPath);
            }
            catch (Exception ex)
            {
                DeleteOwnedFileIfExists(request.TempPdfPath);
                return new PublicationOutcome
                {
                    Status = PublicationOutcomeStatus.PublishFailed,
                    Message = "发布新 PDF 失败: " + ex.Message,
                    OriginalPreserved = false
                };
            }

            return new PublicationOutcome
            {
                Status = PublicationOutcomeStatus.Published,
                Message = "PDF 已发布。",
                OriginalPreserved = false
            };
        }

        private PublicationOutcome ReplaceExisting(PublicationRequest request)
        {
            var backupPath = request.FinalOutputPath + "." + Guid.NewGuid().ToString("N") + ".bak.pdf";

            try
            {
                replacer.ReplaceWithBackup(request.TempPdfPath, request.FinalOutputPath, backupPath);
            }
            catch (Exception ex)
            {
                return FailReplace(request, backupPath, "覆盖 PDF 失败: " + ex.Message);
            }

            if (!File.Exists(request.FinalOutputPath) || new FileInfo(request.FinalOutputPath).Length == 0)
            {
                return FailReplace(request, backupPath, "替换后的 PDF 无效。");
            }

            replacer.DeleteFile(backupPath);
            return new PublicationOutcome
            {
                Status = PublicationOutcomeStatus.Replaced,
                Message = "PDF 已安全覆盖。",
                OriginalPreserved = false
            };
        }

        private PublicationOutcome FailReplace(PublicationRequest request, string backupPath, string reason)
        {
            var restored = RestoreFromBackup(backupPath, request.FinalOutputPath);
            DeleteOwnedFileIfExists(request.TempPdfPath);
            return new PublicationOutcome
            {
                Status = PublicationOutcomeStatus.PublishFailed,
                Message = restored
                    ? reason + " 已恢复原有文件。"
                    : reason + " 且原有文件无法自动恢复，请人工检查目标目录。",
                OriginalPreserved = restored
            };
        }

        private bool RestoreFromBackup(string backupPath, string finalPath)
        {
            if (!File.Exists(backupPath))
            {
                return false;
            }

            try
            {
                replacer.RestoreBackup(backupPath, finalPath);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void DeleteOwnedFileIfExists(string path)
        {
            if (File.Exists(path))
            {
                replacer.DeleteFile(path);
            }
        }
    }
}
