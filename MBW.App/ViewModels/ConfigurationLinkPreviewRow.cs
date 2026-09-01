namespace MBW.App.ViewModels
{
    public sealed class ConfigurationLinkPreviewRow
    {
        public ConfigurationLinkPreviewRow(
            long rowNumber,
            string keyValue,
            string expectedFileName,
            bool isMatched)
        {
            RowNumber = rowNumber;
            KeyValue = keyValue;
            ExpectedFileName = expectedFileName;
            IsMatched = isMatched;
        }

        public long RowNumber { get; }

        public string KeyValue { get; }

        public string ExpectedFileName { get; }

        public bool IsMatched { get; }

        public string FileDisplay => IsMatched ? ExpectedFileName : "—";

        public string StatusLabel => IsMatched ? "Matched" : "Missing";

        public string StatusGlyph => IsMatched ? "\uE73E" : "\uE783";

        public string MissingDisplayLine =>
            $"Row {RowNumber} · {KeyValue} → {ExpectedFileName}";
    }
}
