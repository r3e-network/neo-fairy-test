// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

namespace Neo.Fairy.Testing.Coverage;

/// <summary>
/// Collects code coverage data during test execution.
/// </summary>
public sealed class CoverageCollector
{
    private readonly Dictionary<string, ContractCoverage> _contracts = new(StringComparer.OrdinalIgnoreCase);
    private bool _isCollecting;

    /// <summary>
    /// Gets whether coverage collection is active.
    /// </summary>
    public bool IsCollecting => _isCollecting;

    /// <summary>
    /// Starts collecting coverage data.
    /// </summary>
    public void Start()
    {
        _isCollecting = true;
    }

    /// <summary>
    /// Stops collecting coverage data.
    /// </summary>
    public void Stop()
    {
        _isCollecting = false;
    }

    /// <summary>
    /// Records an instruction execution.
    /// </summary>
    /// <param name="contractHash">The contract hash.</param>
    /// <param name="instructionPointer">The instruction pointer.</param>
    /// <param name="opCode">The opcode executed.</param>
    public void RecordInstruction(string contractHash, int instructionPointer, string opCode)
    {
        if (!_isCollecting) return;

        var coverage = GetOrCreateContractCoverage(contractHash);
        coverage.RecordInstruction(instructionPointer, opCode);
    }

    /// <summary>
    /// Records a source line execution.
    /// </summary>
    /// <param name="contractHash">The contract hash.</param>
    /// <param name="sourceFile">The source file path.</param>
    /// <param name="lineNumber">The line number.</param>
    public void RecordSourceLine(string contractHash, string sourceFile, int lineNumber)
    {
        if (!_isCollecting) return;

        var coverage = GetOrCreateContractCoverage(contractHash);
        coverage.RecordSourceLine(sourceFile, lineNumber);
    }

    /// <summary>
    /// Records a branch execution.
    /// </summary>
    /// <param name="contractHash">The contract hash.</param>
    /// <param name="branchId">The branch identifier.</param>
    /// <param name="taken">Whether the branch was taken.</param>
    public void RecordBranch(string contractHash, string branchId, bool taken)
    {
        if (!_isCollecting) return;

        var coverage = GetOrCreateContractCoverage(contractHash);
        coverage.RecordBranch(branchId, taken);
    }

    /// <summary>
    /// Sets the total instruction count for a contract.
    /// </summary>
    public void SetTotalInstructions(string contractHash, int count)
    {
        var coverage = GetOrCreateContractCoverage(contractHash);
        coverage.TotalInstructions = count;
    }

    /// <summary>
    /// Sets the total source lines for a contract.
    /// </summary>
    public void SetTotalSourceLines(string contractHash, string sourceFile, int count)
    {
        var coverage = GetOrCreateContractCoverage(contractHash);
        coverage.SetTotalSourceLines(sourceFile, count);
    }

    /// <summary>
    /// Gets the coverage report.
    /// </summary>
    public CoverageReport GetReport()
    {
        return new CoverageReport
        {
            Contracts = _contracts.Values.ToList(),
            GeneratedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Resets all coverage data.
    /// </summary>
    public void Reset()
    {
        _contracts.Clear();
    }

    private ContractCoverage GetOrCreateContractCoverage(string contractHash)
    {
        if (!_contracts.TryGetValue(contractHash, out var coverage))
        {
            coverage = new ContractCoverage { ContractHash = contractHash };
            _contracts[contractHash] = coverage;
        }
        return coverage;
    }
}

/// <summary>
/// Coverage data for a single contract.
/// </summary>
public sealed class ContractCoverage
{
    private readonly HashSet<int> _executedInstructions = new();
    private readonly Dictionary<string, HashSet<int>> _executedLines = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _totalLines = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (int TakenCount, int NotTakenCount)> _branches = new();

    /// <summary>
    /// Gets or sets the contract hash.
    /// </summary>
    public required string ContractHash { get; init; }

    /// <summary>
    /// Gets or sets the contract name/alias.
    /// </summary>
    public string? ContractName { get; set; }

    /// <summary>
    /// Gets or sets the total instruction count.
    /// </summary>
    public int TotalInstructions { get; set; }

    /// <summary>
    /// Gets the number of executed instructions.
    /// </summary>
    public int ExecutedInstructions => _executedInstructions.Count;

    /// <summary>
    /// Gets the instruction coverage percentage.
    /// </summary>
    public double InstructionCoverage => TotalInstructions > 0
        ? (double)ExecutedInstructions / TotalInstructions * 100
        : 0;

    /// <summary>
    /// Gets the line coverage by source file.
    /// </summary>
    public IReadOnlyDictionary<string, LineCoverage> LineCoverageByFile
    {
        get
        {
            var result = new Dictionary<string, LineCoverage>();
            foreach (var file in _executedLines.Keys.Union(_totalLines.Keys))
            {
                var executed = _executedLines.TryGetValue(file, out var lines) ? lines.Count : 0;
                var total = _totalLines.TryGetValue(file, out var t) ? t : 0;
                result[file] = new LineCoverage
                {
                    SourceFile = file,
                    ExecutedLines = executed,
                    TotalLines = total,
                    ExecutedLineNumbers = _executedLines.TryGetValue(file, out var nums) ? nums.ToList() : new()
                };
            }
            return result;
        }
    }

    /// <summary>
    /// Gets the overall line coverage percentage.
    /// </summary>
    public double LineCoverage
    {
        get
        {
            var totalExecuted = _executedLines.Values.Sum(s => s.Count);
            var totalLines = _totalLines.Values.Sum();
            return totalLines > 0 ? (double)totalExecuted / totalLines * 100 : 0;
        }
    }

    /// <summary>
    /// Gets the branch coverage percentage.
    /// </summary>
    public double BranchCoverage
    {
        get
        {
            if (_branches.Count == 0) return 100;
            var covered = _branches.Values.Count(b => b.TakenCount > 0 && b.NotTakenCount > 0);
            return (double)covered / _branches.Count * 100;
        }
    }

    internal void RecordInstruction(int instructionPointer, string opCode)
    {
        _executedInstructions.Add(instructionPointer);
    }

    internal void RecordSourceLine(string sourceFile, int lineNumber)
    {
        if (!_executedLines.TryGetValue(sourceFile, out var lines))
        {
            lines = new HashSet<int>();
            _executedLines[sourceFile] = lines;
        }
        lines.Add(lineNumber);
    }

    internal void RecordBranch(string branchId, bool taken)
    {
        if (!_branches.TryGetValue(branchId, out var counts))
        {
            counts = (0, 0);
        }

        if (taken)
            counts = (counts.TakenCount + 1, counts.NotTakenCount);
        else
            counts = (counts.TakenCount, counts.NotTakenCount + 1);

        _branches[branchId] = counts;
    }

    internal void SetTotalSourceLines(string sourceFile, int count)
    {
        _totalLines[sourceFile] = count;
    }
}

/// <summary>
/// Line coverage data for a source file.
/// </summary>
public sealed class LineCoverage
{
    public required string SourceFile { get; init; }
    public required int ExecutedLines { get; init; }
    public required int TotalLines { get; init; }
    public required List<int> ExecutedLineNumbers { get; init; }

    public double Percentage => TotalLines > 0 ? (double)ExecutedLines / TotalLines * 100 : 0;
}

/// <summary>
/// Complete coverage report.
/// </summary>
public sealed class CoverageReport
{
    public required IReadOnlyList<ContractCoverage> Contracts { get; init; }
    public required DateTime GeneratedAt { get; init; }

    /// <summary>
    /// Gets the overall line coverage percentage.
    /// </summary>
    public double OverallLineCoverage
    {
        get
        {
            if (Contracts.Count == 0) return 0;
            return Contracts.Average(c => c.LineCoverage);
        }
    }

    /// <summary>
    /// Gets the overall instruction coverage percentage.
    /// </summary>
    public double OverallInstructionCoverage
    {
        get
        {
            if (Contracts.Count == 0) return 0;
            return Contracts.Average(c => c.InstructionCoverage);
        }
    }

    /// <summary>
    /// Gets the overall branch coverage percentage.
    /// </summary>
    public double OverallBranchCoverage
    {
        get
        {
            if (Contracts.Count == 0) return 0;
            return Contracts.Average(c => c.BranchCoverage);
        }
    }
}
