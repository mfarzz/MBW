using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MBW.Core.Utilities
{
    /// <summary>
    /// Utility for extracting and rendering template variables in format {VariableName}.
    /// </summary>
    public static class TemplateVariableExtractor
    {
        private static readonly Regex VariablePattern = new(@"\{([a-zA-Z_][a-zA-Z0-9_]*)\}", RegexOptions.Compiled);

        /// <summary>
        /// Extract all variable names from template text.
        /// Variables are identified by {VariableName} pattern (alphanumeric + underscore).
        /// </summary>
        /// <param name="template">Template text to extract from (can contain nulls)</param>
        /// <returns>Read-only set of unique variable names (empty set if none found)</returns>
        public static IReadOnlySet<string> ExtractVariables(string? template)
        {
            if (string.IsNullOrEmpty(template))
                return new HashSet<string>();

            var variables = new HashSet<string>();
            var matches = VariablePattern.Matches(template);

            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    var varName = match.Groups[1].Value;
                    variables.Add(varName);
                }
            }

            return variables;
        }

        /// <summary>
        /// Render template by replacing {VariableName} with values from dictionary.
        /// Missing variables are left as-is (no error thrown).
        /// </summary>
        /// <param name="template">Template text with {Variable} placeholders</param>
        /// <param name="variables">Dictionary of variable names and values (case-sensitive)</param>
        /// <returns>Rendered template with substitutions applied</returns>
        public static string RenderTemplate(string? template, IReadOnlyDictionary<string, string>? variables)
        {
            if (string.IsNullOrEmpty(template))
                return template ?? string.Empty;

            if (variables == null || variables.Count == 0)
                return template;

            return VariablePattern.Replace(template, match =>
            {
                var varName = match.Groups[1].Value;
                return variables.TryGetValue(varName, out var value) ? (value ?? string.Empty) : match.Value;
            });
        }
    }
}
