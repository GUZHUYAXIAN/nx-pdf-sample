namespace NxDrawingPdfExporter.Contracts
{
    public static class ResultReasons
    {
        public const string Unknown = nameof(Unknown);
        public const string TargetChanged = nameof(TargetChanged);
        public const string NameConflict = nameof(NameConflict);
        public const string Cancelled = nameof(Cancelled);
        public const string WorkerFailure = nameof(WorkerFailure);
        public const string TemporaryPdfReady = nameof(TemporaryPdfReady);
        public const string PureModel = nameof(PureModel);
        public const string NoValidSheets = nameof(NoValidSheets);
        public const string MissingModel = nameof(MissingModel);
        public const string ViewUpdateFailed = nameof(ViewUpdateFailed);
        public const string SourceChanged = nameof(SourceChanged);
        public const string PartLoadFailed = nameof(PartLoadFailed);
        public const string Ready = nameof(Ready);
        public const string SkippedExisting = nameof(SkippedExisting);
        public const string Published = nameof(Published);
        public const string Overwritten = nameof(Overwritten);
        public const string PublicationFailed = nameof(PublicationFailed);
        public const string DirectoryUnreadable = nameof(DirectoryUnreadable);
        public const string InvalidRevision = nameof(InvalidRevision);
        public const string RevisionAlias = nameof(RevisionAlias);
        public const string RevisionLimit = nameof(RevisionLimit);
        public const string FamilyConflict = nameof(FamilyConflict);
        public const string WorkerResultInvalid = nameof(WorkerResultInvalid);
    }
}
