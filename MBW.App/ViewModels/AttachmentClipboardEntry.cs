namespace MBW.App.ViewModels
{
    internal sealed class AttachmentClipboardEntry
    {
        public AttachmentClipboardEntry(string sourcePath, string name, bool isFolder, bool isCut)
        {
            SourcePath = sourcePath;
            Name = name;
            IsFolder = isFolder;
            IsCut = isCut;
        }

        public string SourcePath { get; }

        public string Name { get; }

        public bool IsFolder { get; }

        public bool IsCut { get; }
    }
}
