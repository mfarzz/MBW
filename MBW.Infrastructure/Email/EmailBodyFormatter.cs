using System.Net;
using System.Text.RegularExpressions;

namespace MBW.Infrastructure.Email
{
    internal static class EmailBodyFormatter
    {
        private static readonly Regex BreakTagRegex = new(
            @"<(br\s*/?|/p|/div|/li|/tr|/h[1-6])\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex HtmlTagRegex = new(
            "<[^>]+>",
            RegexOptions.Compiled);

        private static readonly Regex WhitespaceRegex = new(
            @"[ \t]{2,}",
            RegexOptions.Compiled);

        public static (string Html, string PlainText) Format(string? htmlBody, string? plainTextBody)
        {
            var html = NormalizeHtml(htmlBody);
            var plain = !string.IsNullOrWhiteSpace(plainTextBody)
                ? plainTextBody.Trim()
                : HtmlToPlainText(html);

            return (html, plain);
        }

        internal static string NormalizeHtml(string? htmlBody)
        {
            var content = htmlBody?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(content))
            {
                return string.Empty;
            }

            if (content.Contains("<html", StringComparison.OrdinalIgnoreCase))
            {
                return content;
            }

            return "<!DOCTYPE html>\r\n"
                   + "<html>\r\n"
                   + "<head><meta charset=\"utf-8\"></head>\r\n"
                   + "<body>\r\n"
                   + content
                   + "\r\n</body>\r\n"
                   + "</html>";
        }

        internal static string HtmlToPlainText(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var withBreaks = BreakTagRegex.Replace(html, "\n");
            var withoutTags = HtmlTagRegex.Replace(withBreaks, " ");
            var decoded = WebUtility.HtmlDecode(withoutTags)
                .Replace('\u00A0', ' ');

            var lines = decoded
                .Split('\n')
                .Select(line => WhitespaceRegex.Replace(line.Trim(), " "))
                .Where(line => line.Length > 0);

            return string.Join(Environment.NewLine, lines);
        }
    }
}
