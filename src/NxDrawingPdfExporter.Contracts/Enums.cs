using System.Runtime.Serialization;

namespace NxDrawingPdfExporter.Contracts
{
    public enum ExistingPdfPolicy
    {
        Skip = 0,
        Overwrite = 1
    }

    public enum OutputMode
    {
        BesideSource = 0,
        UnifiedDirectory = 1
    }

    public enum FileResultStatus
    {
        Success = 0,
        Overwritten = 1,
        SkippedExisting = 2,
        PureModel = 3,
        NoValidSheets = 4,
        NameConflict = 5,
        Cancelled = 6,
        Failed = 7
    }

    /// <summary>Raised when a payload violates the versioned job/result protocol.</summary>
    [Serializable]
    public sealed class ProtocolException : Exception
    {
        public ProtocolException(string message)
            : base(message)
        {
        }

        public ProtocolException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
