using System;
using System.Collections.Generic;
using MBW.Core.Utilities;

namespace MBW.Core.Models
{
    public class EmailTemplate
    {
        public string Subject { get; set; } = string.Empty;
        public string HtmlBody { get; set; } = string.Empty;
        public string? PlainTextBody { get; set; }

        public EmailTemplate() { }

        public EmailTemplate(string subject, string htmlBody)
        {
            Subject = subject ?? string.Empty;
            HtmlBody = htmlBody ?? string.Empty;
        }

        /// <summary>
        /// Extract all available variables from this template's Subject and HtmlBody.
        /// Variables are identified by {VariableName} pattern.
        /// </summary>
        /// <returns>Read-only set of unique variable names</returns>
        public IReadOnlySet<string> GetAvailableVariables()
        {
            var subjectVars = TemplateVariableExtractor.ExtractVariables(Subject);
            var bodyVars = TemplateVariableExtractor.ExtractVariables(HtmlBody);

            var combined = new HashSet<string>(subjectVars);
            combined.UnionWith(bodyVars);

            return combined;
        }

        /// <summary>
        /// Render this template for a specific recipient by substituting {Variable} with recipient field values.
        /// Returns a new EmailTemplate with substitutions applied; original template unchanged.
        /// Missing variables are left as-is.
        /// </summary>
        /// <param name="recipient">Recipient row containing field values</param>
        /// <returns>New EmailTemplate with rendered Subject and HtmlBody</returns>
        public EmailTemplate RenderForRecipient(RecipientRow recipient)
        {
            if (recipient == null)
                throw new ArgumentNullException(nameof(recipient));

            var renderedSubject = TemplateVariableExtractor.RenderTemplate(Subject, recipient.Fields);
            var renderedBody = TemplateVariableExtractor.RenderTemplate(HtmlBody, recipient.Fields);
            var renderedPlain = TemplateVariableExtractor.RenderTemplate(PlainTextBody, recipient.Fields);

            return new EmailTemplate(renderedSubject, renderedBody)
            {
                PlainTextBody = renderedPlain
            };
        }
    }
}
