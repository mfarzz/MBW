namespace MBW.Core.Models
{
    public class SmtpSettings
    {
        public string Server { get; set; } = string.Empty;

        public int Port { get; set; } = 587;

        public SmtpSecurityMode Security { get; set; } = SmtpSecurityMode.StartTls;

        public string Username { get; set; } = string.Empty;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Server) && Port > 0;
    }
}
