namespace MBW.Core.Models
{
    public sealed class AttachmentConfiguration
    {
        public const string SharedFolderRelative = "attachments/shared";
        public const string IndividualFolderRelative = "attachments/individual";

        public bool Enabled { get; set; }

        public AttachmentLinkConfiguration Link { get; set; } = AttachmentLinkConfiguration.CreateDefault();

        public static AttachmentConfiguration CreateDefault() => new();

        public AttachmentConfiguration Clone() => new()
        {
            Enabled = Enabled,
            Link = Link?.Clone() ?? AttachmentLinkConfiguration.CreateDefault()
        };
    }
}
