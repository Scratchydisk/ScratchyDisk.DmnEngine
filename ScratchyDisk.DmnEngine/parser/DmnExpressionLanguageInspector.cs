using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace ScratchyDisk.DmnEngine.Parser
{
    /// <summary>
    /// Best-effort inspection of the expression languages declared by a DMN XML document.
    /// </summary>
    public static class DmnExpressionLanguageInspector
    {
        public sealed class Result
        {
            public string ExpressionLanguage { get; init; } = "FEEL";
            public bool IsSupported => ExpressionLanguage == "FEEL";
        }

        public static Result Detect(string xmlContent)
        {
            var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var reader = XmlReader.Create(new StringReader(xmlContent));
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element) continue;
                    var language = reader.GetAttribute("expressionLanguage");
                    if (!string.IsNullOrWhiteSpace(language))
                    {
                        if (string.Equals(language.Trim(), DmnExpressionLanguage.BFeel, StringComparison.OrdinalIgnoreCase))
                            languages.Add("B-FEEL");
                        else if (string.Equals(language.Trim(), "ONNX", StringComparison.OrdinalIgnoreCase))
                            languages.Add("ONNX");
                        else
                            languages.Add("FEEL");
                    }
                    if (string.Equals(reader.GetAttribute("kind"), "ONNX", StringComparison.OrdinalIgnoreCase))
                        languages.Add("ONNX");
                }
            }
            catch
            {
                // Best-effort detection; parsing will report malformed XML separately.
            }

            if (languages.Count == 0) return new Result();
            if (languages.Count == 1) return new Result { ExpressionLanguage = languages.First() };
            return new Result { ExpressionLanguage = "mixed" };
        }
    }
}
