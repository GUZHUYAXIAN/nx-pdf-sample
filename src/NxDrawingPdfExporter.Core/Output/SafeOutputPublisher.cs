using System;
using System.IO;
using NxDrawingPdfExporter.Contracts;
using NxDrawingPdfExporter.Core.Pdf;

namespace NxDrawingPdfExporter.Core.Output;

public enum PublicationOutcomeStatus { Published, Replaced, ValidationFailed, PublishFailed, TargetChanged }

public sealed class PublicationRequest
{
    public string TempPdfPath { get; set; } = "";
    public string FinalOutputPath { get; set; } = "";
    public int ExpectedPageCount { get; set; }
    public PlannedPublishIntent Intent { get; set; }
    public TargetSnapshot? ExpectedTarget { get; set; }
}

public sealed class PublicationOutcome
{
    public PublicationOutcomeStatus Status { get; set; }
    public string Message { get; set; } = "";
    public string ReasonCode { get; set; } = ResultReasons.PublicationFailed;
    public string TechnicalDetails { get; set; } = "";
    public string? BackupPath { get; set; }
    public bool OriginalPreserved { get; set; }
}

public sealed class SafeOutputPublisher
{
    private readonly IPdfInspector inspector;
    private readonly IFileReplacer replacer;
    private readonly ITargetStateReader targets;

    public SafeOutputPublisher(IPdfInspector inspector, IFileReplacer replacer, ITargetStateReader? targets = null)
    {
        this.inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
        this.replacer = replacer ?? throw new ArgumentNullException(nameof(replacer));
        this.targets = targets ?? new PhysicalTargetStateReader();
    }

    public PublicationOutcome Publish(PublicationRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));
        var expected = request.ExpectedTarget;
        if (string.IsNullOrWhiteSpace(request.TempPdfPath) || string.IsNullOrWhiteSpace(request.FinalOutputPath)
            || string.Equals(Path.GetFullPath(request.TempPdfPath), Path.GetFullPath(request.FinalOutputPath), StringComparison.OrdinalIgnoreCase)
            || request.ExpectedPageCount < 1 || expected is null
            || request.Intent != (expected.Exists ? PlannedPublishIntent.ReplaceExpected : PlannedPublishIntent.CreateNew))
            throw new ArgumentException("Invalid frozen publication request.", nameof(request));

        string? backup = null;
        try
        {
            var candidate = targets.Capture(request.TempPdfPath);
            var problem = !candidate.Exists ? "临时 PDF 不存在。"
                : candidate.Length == 0 ? "临时 PDF 是空文件。"
                : ValidatePdfContent(request.TempPdfPath, request.ExpectedPageCount);
            if (problem is not null)
                return Fail(request, PublicationOutcomeStatus.ValidationFailed, problem, expected.Exists);
            if (!candidate.Matches(targets.Capture(request.TempPdfPath)))
                return Fail(request, PublicationOutcomeStatus.ValidationFailed, "临时 PDF 在校验期间变化。");
            if (!expected.Matches(targets.Capture(request.FinalOutputPath)))
                return Fail(request, PublicationOutcomeStatus.TargetChanged, "目标已变化，请重新检查。");

            if (request.Intent == PlannedPublishIntent.CreateNew)
            {
                try
                {
                    replacer.MoveIntoPlace(request.TempPdfPath, request.FinalOutputPath);
                }
                catch (IOException) when (File.Exists(request.FinalOutputPath))
                {
                    return Fail(request, PublicationOutcomeStatus.TargetChanged, "目标已变化，请重新检查。");
                }
                return new PublicationOutcome { Status = PublicationOutcomeStatus.Published, ReasonCode = ResultReasons.Published, Message = "PDF 已发布。" };
            }

            backup = request.FinalOutputPath + "." + Guid.NewGuid().ToString("N") + ".bak.pdf";
            replacer.ReplaceWithBackup(request.TempPdfPath, request.FinalOutputPath, backup);
            if (!expected.Matches(targets.Capture(backup)))
                return Fail(request, PublicationOutcomeStatus.TargetChanged, "替换期间目标发生变化，请检查备份。", backup: backup);

            // Keep the final name stable until inspection and backup cleanup are complete.
            // A writer already holding an incompatible handle causes the backup to be retained.
            using var protectedTarget = new FileStream(request.FinalOutputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            problem = ValidatePdfContent(request.FinalOutputPath, request.ExpectedPageCount);
            var published = targets.Capture(request.FinalOutputPath);
            if (problem is not null || !candidate.MatchesContent(published))
                return Fail(request, PublicationOutcomeStatus.PublishFailed, problem ?? "替换后内容不一致。", backup: backup);

            replacer.DeleteFile(backup);
            return new PublicationOutcome { Status = PublicationOutcomeStatus.Replaced, ReasonCode = ResultReasons.Overwritten, Message = "PDF 已安全覆盖。" };
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            // Only a non-overwriting move can recover a missing target. If another writer
            // owns the name, keep the backup and leave their file for manual reconciliation.
            string details = error.ToString();
            bool recovered = false;
            if (backup is not null && File.Exists(backup) && !File.Exists(request.FinalOutputPath))
            {
                try
                {
                    if (expected.Matches(targets.Capture(backup)))
                    {
                        replacer.MoveIntoPlace(backup, request.FinalOutputPath);
                        recovered = true;
                    }
                }
                catch (Exception restoreError) when (restoreError is IOException || restoreError is UnauthorizedAccessException)
                {
                    details += Environment.NewLine + restoreError;
                }
            }
            return Fail(request, PublicationOutcomeStatus.PublishFailed, "发布 PDF 失败: " + error.Message,
                recovered, backup, details);
        }
    }

    private string? ValidatePdfContent(string path, int expectedPageCount)
    {
        PdfInspection inspection;
        try
        {
            inspection = inspector.Inspect(path);
        }
        catch (Exception error)
        {
            return "PDF 校验失败: " + error.Message;
        }
        if (!inspection.HasPdfHeader)
            return "PDF 不是有效的 PDF 文件（缺少 PDF 文件头）。";
        return inspection.PageCount != expectedPageCount
            ? $"PDF 页数 {inspection.PageCount} 与有效图纸页数 {expectedPageCount} 不一致。" : null;
    }

    private PublicationOutcome Fail(PublicationRequest request, PublicationOutcomeStatus status, string message,
        bool originalPreserved = false, string? backup = null, string details = "")
    {
        try
        {
            if (File.Exists(request.TempPdfPath))
                replacer.DeleteFile(request.TempPdfPath);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
        {
            details += Environment.NewLine + error;
        }

        var retainedBackup = backup is not null && File.Exists(backup) ? backup : null;
        return new PublicationOutcome
        {
            Status = status,
            Message = message,
            ReasonCode = status == PublicationOutcomeStatus.TargetChanged ? ResultReasons.TargetChanged : ResultReasons.PublicationFailed,
            TechnicalDetails = string.Join(Environment.NewLine, new[] { message, details, retainedBackup ?? "" }),
            OriginalPreserved = originalPreserved,
            BackupPath = retainedBackup
        };
    }
}
