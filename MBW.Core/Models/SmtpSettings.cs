namespace MBW.Core.Models
{
    public class SmtpSettings
    {
        public string FromName { get; set; } = string.Empty;

        public string FromEmail { get; set; } = string.Empty;

        public bool UseReplyToAddress { get; set; }

        public string ReplyToEmail { get; set; } = string.Empty;

        public string Server { get; set; } = string.Empty;

        public int Port { get; set; } = 587;

        public SmtpSecurityMode Security { get; set; } = SmtpSecurityMode.StartTls;

        public bool RequiresAuthentication { get; set; } = true;

        public string Username { get; set; } = string.Empty;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Server) && Port > 0;

        public string GetSenderEmail() =>
            !string.IsNullOrWhiteSpace(FromEmail)
                ? FromEmail.Trim()
                : (!string.IsNullOrWhiteSpace(Username) ? Username.Trim() : string.Empty);

        public string GetSenderDisplay()
        {
            var email = GetSenderEmail();
            if (string.IsNullOrWhiteSpace(email))
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(FromName)
                ? email
                : $"{FromName.Trim()} <{email}>";
        }
    }
}
