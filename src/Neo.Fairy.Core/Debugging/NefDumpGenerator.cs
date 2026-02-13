// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Neo.SmartContract;
using Neo.VM;

namespace Neo.Fairy.Core.Debugging;

/// <summary>
/// Generates DumpNef-style assembly text from a NEF file and its .nefdbgnfo debug info.
/// This enables source-level debugging and coverage without requiring an external dumpnef tool.
/// </summary>
public static class NefDumpGenerator
{
    private static readonly Regex DocumentRegex = new(@"\[(\d+)\](\d+)\:(\d+)\-(\d+)\:(\d+)", RegexOptions.Compiled);
    private static readonly Regex RangeRegex = new(@"(\d+)\-(\d+)", RegexOptions.Compiled);
    private static readonly Regex SequencePointRegex = new(@"(\d+)(\[\d+\]\d+\:\d+\-\d+\:\d+)", RegexOptions.Compiled);

    private sealed record SequencePoint(
        int DocumentId,
        int StartLine,
        int StartColumn,
        int EndLine,
        int EndColumn);

    /// <summary>
    /// Tries to generate DumpNef text.
    /// Returns false if debug info is missing/invalid or source files cannot be resolved.
    /// </summary>
    public static bool TryGenerateDumpNef(
        byte[] nefBytes,
        byte[] debugInfoBytes,
        string? baseDirectory,
        out string dumpText,
        out string? error)
    {
        dumpText = string.Empty;
        error = null;

        try
        {
            var nef = NefFile.Parse(nefBytes);
            var debugJson = UnzipDebugInfo(debugInfoBytes);
            using var debugDoc = JsonDocument.Parse(debugJson);

            var root = debugDoc.RootElement;
            if (!root.TryGetProperty("methods", out var methodsElement) ||
                methodsElement.ValueKind != JsonValueKind.Array)
            {
                error = "Debug info missing methods.";
                return false;
            }

            var documents = root.TryGetProperty("documents", out var docsElement) &&
                            docsElement.ValueKind == JsonValueKind.Array
                ? docsElement.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray()
                : Array.Empty<string>();

            var documentRoot = root.TryGetProperty("document-root", out var docRootElement)
                ? docRootElement.GetString()
                : null;

            if (!string.IsNullOrWhiteSpace(documentRoot) && !Path.IsPathRooted(documentRoot) && baseDirectory != null)
            {
                documentRoot = Path.GetFullPath(Path.Combine(baseDirectory, documentRoot));
            }

            if (string.IsNullOrWhiteSpace(documentRoot))
            {
                documentRoot = baseDirectory;
            }

            var methodStartAddrToName = new Dictionary<int, string>();
            var methodEndAddrToName = new Dictionary<int, string>();
            var addrToSequencePoints = new Dictionary<int, List<SequencePoint>>();

            foreach (var method in methodsElement.EnumerateArray())
            {
                var rangeString = method.TryGetProperty("range", out var rangeElement)
                    ? rangeElement.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(rangeString))
                    continue;

                var rangeMatch = RangeRegex.Match(rangeString);
                if (!rangeMatch.Success)
                    continue;

                var startAddr = int.Parse(rangeMatch.Groups[1].Value);
                var endAddr = int.Parse(rangeMatch.Groups[2].Value);

                var id = method.TryGetProperty("id", out var idElement)
                    ? idElement.GetString()
                    : null;

                var name = id ?? $"method_{startAddr}";

                methodStartAddrToName.TryAdd(startAddr, name);
                methodEndAddrToName.TryAdd(endAddr, name);

                if (!method.TryGetProperty("sequence-points", out var seqElement) ||
                    seqElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var seqPointEl in seqElement.EnumerateArray())
                {
                    var seqPointStr = seqPointEl.GetString();
                    if (string.IsNullOrWhiteSpace(seqPointStr))
                        continue;

                    var seqMatch = SequencePointRegex.Match(seqPointStr);
                    if (!seqMatch.Success)
                        continue;

                    var addr = int.Parse(seqMatch.Groups[1].Value);
                    var docMatch = DocumentRegex.Match(seqMatch.Groups[2].Value);
                    if (!docMatch.Success)
                        continue;

                    var sp = new SequencePoint(
                        DocumentId: int.Parse(docMatch.Groups[1].Value),
                        StartLine: int.Parse(docMatch.Groups[2].Value),
                        StartColumn: int.Parse(docMatch.Groups[3].Value),
                        EndLine: int.Parse(docMatch.Groups[4].Value),
                        EndColumn: int.Parse(docMatch.Groups[5].Value));

                    if (!addrToSequencePoints.TryGetValue(addr, out var list))
                    {
                        list = new List<SequencePoint>();
                        addrToSequencePoints[addr] = list;
                    }

                    list.Add(sp);
                }
            }

            var script = new Script(nef.Script);
            var contentCache = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            var builder = new StringBuilder();

            for (var ip = 0; ip < script.Length;)
            {
                if (methodStartAddrToName.TryGetValue(ip, out var startName))
                    builder.AppendLine($"# Method Start {startName}");

                if (methodEndAddrToName.TryGetValue(ip, out var endName))
                    builder.AppendLine($"# Method End {endName}");

                if (addrToSequencePoints.TryGetValue(ip, out var seqPoints))
                {
                    foreach (var sp in seqPoints)
                    {
                        var docPath = ResolveDocumentPath(documents, sp.DocumentId, documentRoot);
                        if (docPath == null)
                            continue;

                        if (!contentCache.TryGetValue(docPath, out var lines))
                        {
                            if (!File.Exists(docPath))
                                continue;
                            lines = File.ReadAllLines(docPath);
                            contentCache[docPath] = lines;
                        }

                        EmitSourceLines(builder, Path.GetFileName(docPath), lines, sp);
                    }
                }

                var instruction = script.GetInstruction(ip);
                builder.AppendLine(FormatInstruction(ip, instruction));
                ip += instruction.Size;
            }

            dumpText = builder.ToString();
            return dumpText.Length > 0;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string? ResolveDocumentPath(string[] documents, int documentId, string? documentRoot)
    {
        if (documentId < 0 || documentId >= documents.Length)
            return null;

        var path = documents[documentId];
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!string.IsNullOrWhiteSpace(documentRoot))
            return Path.GetFullPath(Path.Combine(documentRoot, path));

        return Path.GetFullPath(path);
    }

    private static void EmitSourceLines(
        StringBuilder builder,
        string filename,
        string[] lines,
        SequencePoint sp)
    {
        if (sp.StartLine <= 0 || sp.StartLine > lines.Length)
            return;

        var startLine = sp.StartLine;
        var endLine = Math.Min(sp.EndLine, lines.Length);

        if (startLine == endLine)
        {
            var lineText = SafeSlice(lines[startLine - 1], sp.StartColumn - 1, sp.EndColumn - 1);
            builder.AppendLine($"# Code {filename} line {startLine}: \"{lineText}\"");
            return;
        }

        for (var lineIndex = startLine; lineIndex <= endLine; lineIndex++)
        {
            var text = lines[lineIndex - 1];
            string segment;
            if (lineIndex == startLine)
            {
                segment = sp.StartColumn > 1 && sp.StartColumn - 1 < text.Length
                    ? text[(sp.StartColumn - 1)..].Trim()
                    : text.Trim();
            }
            else if (lineIndex == endLine)
            {
                segment = sp.EndColumn > 1 && sp.EndColumn - 1 <= text.Length
                    ? text[..(sp.EndColumn - 1)].Trim()
                    : text.Trim();
            }
            else
            {
                segment = text.Trim();
            }

            builder.AppendLine($"# Code {filename} line {lineIndex}: \"{segment}\"");
        }
    }

    private static string SafeSlice(string source, int start, int end)
    {
        if (start < 0) start = 0;
        if (end < start) end = start;
        if (start >= source.Length) return string.Empty;
        if (end > source.Length) end = source.Length;
        return source[start..end].Trim();
    }

    private static string FormatInstruction(int address, Instruction instruction)
    {
        var sb = new StringBuilder();
        sb.Append(address);
        sb.Append(' ');
        sb.Append(instruction.OpCode);

        if (!instruction.Operand.IsEmpty)
        {
            sb.Append(' ');
            sb.Append(BitConverter.ToString(instruction.Operand.ToArray()));
        }

        return sb.ToString();
    }

    private static string UnzipDebugInfo(byte[] zippedBuffer)
    {
        using var zippedStream = new MemoryStream(zippedBuffer);
        using var archive = new ZipArchive(zippedStream, ZipArchiveMode.Read, false, Encoding.UTF8);
        var entry = archive.Entries.FirstOrDefault();
        if (entry == null)
            throw new ArgumentException("No file found in debug info archive.");

        using var unzippedEntryStream = entry.Open();
        using var ms = new MemoryStream();
        unzippedEntryStream.CopyTo(ms);
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}

