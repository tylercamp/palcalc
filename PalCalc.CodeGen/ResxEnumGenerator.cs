using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace PalCalc.CodeGen
{
    [Generator]
    public class ResxEnumGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        // praise be to chatgpt
        public void Execute(GeneratorExecutionContext context)
        {
            // Find the resx file from AdditionalFiles
            var resxFile = context.AdditionalFiles.FirstOrDefault(file => file.Path.EndsWith("LocalizationCodes.resx"));

            if (resxFile == null)
                return;

            // Read the resx file
            var resxContent = resxFile.GetText(context.CancellationToken)?.ToString();

            if (string.IsNullOrEmpty(resxContent))
                return;

            // Parse the resx file
            var xdoc = XDocument.Parse(resxContent);
            var entries = xdoc.Descendants("data")
                .Select(e => e.Attribute("name")?.Value)
                .Where(name => !string.IsNullOrEmpty(name))
                .Distinct()
                .ToList();

            if (entries.Count == 0)
                return;

            // Generate the enum source code
            var sb = new StringBuilder();
            sb.AppendLine("namespace YourNamespace");
            sb.AppendLine("{");
            sb.AppendLine("    public enum LocalizationCodes");
            sb.AppendLine("    {");

            foreach (var entry in entries)
            {
                sb.AppendLine($"        {entry},");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            // Add the generated source code to the compilation
            context.AddSource("LocalizationCodesEnum.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }
    }
}
