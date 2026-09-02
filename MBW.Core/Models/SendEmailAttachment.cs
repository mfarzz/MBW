namespace MBW.Core.Models
{
    public sealed class SendEmailAttachment
    {
        public SendEmailAttachment(string filePath, string fileName)
        {
            FilePath = filePath ?? throw new System.ArgumentNullException(nameof(filePath));
            FileName = fileName ?? throw new System.ArgumentNullException(nameof(fileName));
        }

        public string FilePath { get; }

        public string FileName { get; }
    }
}
