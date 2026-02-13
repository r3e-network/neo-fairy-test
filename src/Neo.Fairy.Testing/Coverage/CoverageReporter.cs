// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;

namespace Neo.Fairy.Testing.Coverage;

/// <summary>
/// Generates coverage reports in various formats.
/// </summary>
public static class CoverageReporter
{
    /// <summary>
    /// Generates a console-friendly summary report.
    /// </summary>
    public static string GenerateConsoleSummary(CoverageReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("                      COVERAGE REPORT                          ");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();

        foreach (var contract in report.Contracts)
        {
            var name = contract.ContractName
                ?? (contract.ContractHash.Length > 10 ? contract.ContractHash[..10] + "..." : contract.ContractHash);
            sb.AppendLine($"Contract: {name}");
            sb.AppendLine($"  Lines:        {contract.LineCoverage:F1}%");
            sb.AppendLine($"  Instructions: {contract.InstructionCoverage:F1}%");
            sb.AppendLine($"  Branches:     {contract.BranchCoverage:F1}%");

            foreach (var (file, coverage) in contract.LineCoverageByFile)
            {
                var fileName = Path.GetFileName(file);
                sb.AppendLine($"    {fileName}: {coverage.ExecutedLines}/{coverage.TotalLines} ({coverage.Percentage:F1}%)");
            }

            sb.AppendLine();
        }

        sb.AppendLine("───────────────────────────────────────────────────────────────");
        sb.AppendLine($"Overall Coverage: {report.OverallLineCoverage:F1}%");
        sb.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");

        return sb.ToString();
    }

    /// <summary>
    /// Generates an LCOV format report for integration with coverage tools.
    /// </summary>
    public static string GenerateLcov(CoverageReport report)
    {
        var sb = new StringBuilder();

        foreach (var contract in report.Contracts)
        {
            foreach (var (file, coverage) in contract.LineCoverageByFile)
            {
                sb.AppendLine("TN:"); // Test name (empty)
                sb.AppendLine($"SF:{file}");

                // Line coverage data
                foreach (var lineNum in coverage.ExecutedLineNumbers.OrderBy(n => n))
                {
                    sb.AppendLine($"DA:{lineNum},1");
                }

                // Summary
                sb.AppendLine($"LF:{coverage.TotalLines}");
                sb.AppendLine($"LH:{coverage.ExecutedLines}");
                sb.AppendLine("end_of_record");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates an HTML coverage report.
    /// </summary>
    public static string GenerateHtml(CoverageReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("  <title>Neo Fairy Coverage Report</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; margin: 0; padding: 20px; background: #f5f5f5; }");
        sb.AppendLine("    .container { max-width: 1200px; margin: 0 auto; }");
        sb.AppendLine("    h1 { color: #333; border-bottom: 2px solid #00e599; padding-bottom: 10px; }");
        sb.AppendLine("    .summary { background: white; padding: 20px; border-radius: 8px; margin-bottom: 20px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
        sb.AppendLine("    .summary h2 { margin-top: 0; color: #00e599; }");
        sb.AppendLine("    .metric { display: inline-block; margin-right: 30px; }");
        sb.AppendLine("    .metric-value { font-size: 2em; font-weight: bold; }");
        sb.AppendLine("    .metric-label { color: #666; }");
        sb.AppendLine("    .contract { background: white; padding: 20px; border-radius: 8px; margin-bottom: 15px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
        sb.AppendLine("    .contract h3 { margin-top: 0; color: #333; }");
        sb.AppendLine("    .progress { background: #e0e0e0; border-radius: 4px; height: 20px; overflow: hidden; }");
        sb.AppendLine("    .progress-bar { height: 100%; transition: width 0.3s; }");
        sb.AppendLine("    .progress-bar.high { background: #4caf50; }");
        sb.AppendLine("    .progress-bar.medium { background: #ff9800; }");
        sb.AppendLine("    .progress-bar.low { background: #f44336; }");
        sb.AppendLine("    table { width: 100%; border-collapse: collapse; margin-top: 10px; }");
        sb.AppendLine("    th, td { padding: 8px 12px; text-align: left; border-bottom: 1px solid #e0e0e0; }");
        sb.AppendLine("    th { background: #f5f5f5; font-weight: 600; }");
        sb.AppendLine("    .footer { text-align: center; color: #666; margin-top: 30px; font-size: 0.9em; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"container\">");
        sb.AppendLine("    <h1>🧚 Neo Fairy Coverage Report</h1>");

        // Summary section
        sb.AppendLine("    <div class=\"summary\">");
        sb.AppendLine("      <h2>Overall Coverage</h2>");
        sb.AppendLine($"      <div class=\"metric\"><div class=\"metric-value\">{report.OverallLineCoverage:F1}%</div><div class=\"metric-label\">Line Coverage</div></div>");
        sb.AppendLine($"      <div class=\"metric\"><div class=\"metric-value\">{report.OverallInstructionCoverage:F1}%</div><div class=\"metric-label\">Instruction Coverage</div></div>");
        sb.AppendLine($"      <div class=\"metric\"><div class=\"metric-value\">{report.OverallBranchCoverage:F1}%</div><div class=\"metric-label\">Branch Coverage</div></div>");
        sb.AppendLine("    </div>");

        // Contract details
        foreach (var contract in report.Contracts)
        {
            var name = contract.ContractName ?? contract.ContractHash;
            var coverageClass = contract.LineCoverage >= 80 ? "high" : contract.LineCoverage >= 50 ? "medium" : "low";

            sb.AppendLine("    <div class=\"contract\">");
            sb.AppendLine($"      <h3>{EscapeHtml(name)}</h3>");
            sb.AppendLine($"      <div class=\"progress\"><div class=\"progress-bar {coverageClass}\" style=\"width: {contract.LineCoverage:F0}%\"></div></div>");
            sb.AppendLine($"      <p>Lines: {contract.LineCoverage:F1}% | Instructions: {contract.InstructionCoverage:F1}% | Branches: {contract.BranchCoverage:F1}%</p>");

            if (contract.LineCoverageByFile.Count > 0)
            {
                sb.AppendLine("      <table>");
                sb.AppendLine("        <tr><th>File</th><th>Lines</th><th>Coverage</th></tr>");

                foreach (var (file, coverage) in contract.LineCoverageByFile)
                {
                    var fileName = Path.GetFileName(file);
                    sb.AppendLine($"        <tr><td>{EscapeHtml(fileName)}</td><td>{coverage.ExecutedLines}/{coverage.TotalLines}</td><td>{coverage.Percentage:F1}%</td></tr>");
                }

                sb.AppendLine("      </table>");
            }

            sb.AppendLine("    </div>");
        }

        sb.AppendLine($"    <div class=\"footer\">Generated by Neo Fairy Framework on {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC</div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a JSON coverage report.
    /// </summary>
    public static string GenerateJson(CoverageReport report)
    {
        var data = new
        {
            generatedAt = report.GeneratedAt,
            overallLineCoverage = Math.Round(report.OverallLineCoverage, 2),
            overallInstructionCoverage = Math.Round(report.OverallInstructionCoverage, 2),
            overallBranchCoverage = Math.Round(report.OverallBranchCoverage, 2),
            contracts = report.Contracts.Select(c => new
            {
                hash = c.ContractHash,
                name = c.ContractName,
                lineCoverage = Math.Round(c.LineCoverage, 2),
                instructionCoverage = Math.Round(c.InstructionCoverage, 2),
                branchCoverage = Math.Round(c.BranchCoverage, 2),
                executedInstructions = c.ExecutedInstructions,
                totalInstructions = c.TotalInstructions,
                files = c.LineCoverageByFile.Select(f => new
                {
                    path = f.Key,
                    executedLines = f.Value.ExecutedLines,
                    totalLines = f.Value.TotalLines,
                    coverage = Math.Round(f.Value.Percentage, 2)
                }).ToArray()
            }).ToArray()
        };

        return JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <summary>
    /// Writes coverage report to files in the specified directory.
    /// </summary>
    public static void WriteReports(CoverageReport report, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        // Write all formats
        File.WriteAllText(Path.Combine(outputDirectory, "coverage.txt"), GenerateConsoleSummary(report));
        File.WriteAllText(Path.Combine(outputDirectory, "lcov.info"), GenerateLcov(report));
        File.WriteAllText(Path.Combine(outputDirectory, "coverage.html"), GenerateHtml(report));
        File.WriteAllText(Path.Combine(outputDirectory, "coverage.json"), GenerateJson(report));
    }

    private static string EscapeHtml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
