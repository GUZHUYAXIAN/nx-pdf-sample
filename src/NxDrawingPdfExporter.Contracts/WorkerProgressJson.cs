using System;
using System.IO;
using System.Text;

namespace NxDrawingPdfExporter.Contracts;

/// <summary>Advisory data only. This type never participates in validating a JobResult.</summary>
public static class WorkerProgressJson
{
    public const string FileName = "progress.json";
    public const int MaximumBytes = 128 * 1024;

    public static string Serialize(WorkerProgressSnapshot value) => JobJsonSerializer.Serialize(value);

    public static WorkerProgressSnapshot Deserialize(string json) => JobJsonSerializer.Deserialize<WorkerProgressSnapshot>(json);

    public static void WriteAtomic(WorkerProgressSnapshot value, string path) => JobJsonSerializer.WriteToFile(value, path);

    public static WorkerProgressSnapshot ReadBounded(string path, int maximumBytes = MaximumBytes)
    {
        if (maximumBytes < 1 || maximumBytes > MaximumBytes)
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (file.Length > maximumBytes)
            throw new ProtocolException("Progress snapshot exceeds its size limit.");
        var bytes = new byte[maximumBytes + 1];
        var length = 0;
        while (length < bytes.Length)
        {
            var read = file.Read(bytes, length, bytes.Length - length);
            if (read == 0)
                break;
            length += read;
        }
        if (length > maximumBytes)
            throw new ProtocolException("Progress snapshot exceeds its size limit.");
        return Deserialize(Encoding.UTF8.GetString(bytes, 0, length));
    }

    public static bool ValidateForRun(WorkerProgressSnapshot value, string runId, int total, long lastSequence)
    {
        if (value.SchemaVersion != 1 || string.IsNullOrWhiteSpace(value.RunId) || value.RunId != runId
            || value.Sequence <= Math.Max(0, lastSequence) || !Enum.IsDefined(typeof(WorkerProgressStage), value.Stage)
            || value.Total != total || value.Total < 0 || value.Processed < 0 || value.Processed > total
            || value.Generated < 0 || value.Failed < 0 || value.PureModel < 0 || value.NoValidSheets < 0 || value.OtherProcessed < 0
            || (long)value.Generated + value.Failed + value.PureModel + value.NoValidSheets + value.OtherProcessed != value.Processed)
            return false;
        var active = value.Stage >= WorkerProgressStage.CheckingItem && value.Stage <= WorkerProgressStage.Closing;
        if (value.Stage is WorkerProgressStage.Initializing or WorkerProgressStage.Ready && value.Processed != 0)
            return false;
        return active ? value.Processed < total && value.CurrentItemIndex == value.Processed : value.CurrentItemIndex is null;
    }
}
