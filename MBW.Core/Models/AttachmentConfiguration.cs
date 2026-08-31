namespace MBW.Core.Models
{
    public sealed class AttachmentConfiguration
    {
        public const string SharedFolderRelative = "attachments/shared";
        public const string IndividualFolderRelative = "attachments/individual";

        public bool Enabled { get; set; }

        public static AttachmentConfiguration CreateDefault() => new();

        public AttachmentConfiguration Clone() => new() { Enabled = Enabled };
    }
}
