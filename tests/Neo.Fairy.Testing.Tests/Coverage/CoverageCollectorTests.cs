// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using FluentAssertions;
using Neo.Fairy.Testing.Coverage;
using Xunit;

namespace Neo.Fairy.Testing.Tests.Coverage;

public class CoverageCollectorTests
{
    [Fact]
    public void Start_SetsIsCollectingToTrue()
    {
        // Arrange
        var collector = new CoverageCollector();

        // Act
        collector.Start();

        // Assert
        collector.IsCollecting.Should().BeTrue();
    }

    [Fact]
    public void Stop_SetsIsCollectingToFalse()
    {
        // Arrange
        var collector = new CoverageCollector();
        collector.Start();

        // Act
        collector.Stop();

        // Assert
        collector.IsCollecting.Should().BeFalse();
    }

    [Fact]
    public void RecordInstruction_WhenCollecting_RecordsData()
    {
        // Arrange
        var collector = new CoverageCollector();
        collector.SetTotalInstructions("0x1234", 100);
        collector.Start();

        // Act
        collector.RecordInstruction("0x1234", 0, "PUSH1");
        collector.RecordInstruction("0x1234", 1, "PUSH2");
        collector.RecordInstruction("0x1234", 2, "ADD");

        // Assert
        var report = collector.GetReport();
        report.Contracts.Should().HaveCount(1);
        report.Contracts[0].ExecutedInstructions.Should().Be(3);
    }

    [Fact]
    public void RecordInstruction_WhenNotCollecting_DoesNotRecord()
    {
        // Arrange
        var collector = new CoverageCollector();
        // Not calling Start()

        // Act
        collector.RecordInstruction("0x1234", 0, "PUSH1");

        // Assert
        var report = collector.GetReport();
        // When not collecting, no contracts should be created
        report.Contracts.Should().BeEmpty();
    }

    [Fact]
    public void RecordSourceLine_RecordsLineNumbers()
    {
        // Arrange
        var collector = new CoverageCollector();
        collector.SetTotalSourceLines("0x1234", "Contract.cs", 50);
        collector.Start();

        // Act
        collector.RecordSourceLine("0x1234", "Contract.cs", 10);
        collector.RecordSourceLine("0x1234", "Contract.cs", 15);
        collector.RecordSourceLine("0x1234", "Contract.cs", 20);

        // Assert
        var report = collector.GetReport();
        var contract = report.Contracts[0];
        contract.LineCoverageByFile.Should().ContainKey("Contract.cs");
        contract.LineCoverageByFile["Contract.cs"].ExecutedLines.Should().Be(3);
    }

    [Fact]
    public void RecordSourceLine_DuplicateLines_CountsOnce()
    {
        // Arrange
        var collector = new CoverageCollector();
        collector.SetTotalSourceLines("0x1234", "Contract.cs", 50);
        collector.Start();

        // Act
        collector.RecordSourceLine("0x1234", "Contract.cs", 10);
        collector.RecordSourceLine("0x1234", "Contract.cs", 10);
        collector.RecordSourceLine("0x1234", "Contract.cs", 10);

        // Assert
        var report = collector.GetReport();
        var contract = report.Contracts[0];
        contract.LineCoverageByFile["Contract.cs"].ExecutedLines.Should().Be(1);
    }

    [Fact]
    public void RecordBranch_TracksBranchCoverage()
    {
        // Arrange
        var collector = new CoverageCollector();
        collector.Start();

        // Act
        collector.RecordBranch("0x1234", "branch_1", true);
        collector.RecordBranch("0x1234", "branch_1", false);
        collector.RecordBranch("0x1234", "branch_2", true);

        // Assert
        var report = collector.GetReport();
        var contract = report.Contracts[0];
        // branch_1 is fully covered (both taken and not taken)
        // branch_2 is partially covered (only taken)
        contract.BranchCoverage.Should().Be(50); // 1 out of 2 branches fully covered
    }

    [Fact]
    public void InstructionCoverage_CalculatesPercentage()
    {
        // Arrange
        var collector = new CoverageCollector();
        collector.SetTotalInstructions("0x1234", 10);
        collector.Start();

        // Act
        collector.RecordInstruction("0x1234", 0, "OP1");
        collector.RecordInstruction("0x1234", 1, "OP2");
        collector.RecordInstruction("0x1234", 2, "OP3");

        // Assert
        var report = collector.GetReport();
        report.Contracts[0].InstructionCoverage.Should().Be(30); // 3/10 = 30%
    }

    [Fact]
    public void LineCoverage_CalculatesPercentage()
    {
        // Arrange
        var collector = new CoverageCollector();
        collector.SetTotalSourceLines("0x1234", "Contract.cs", 20);
        collector.Start();

        // Act
        collector.RecordSourceLine("0x1234", "Contract.cs", 1);
        collector.RecordSourceLine("0x1234", "Contract.cs", 2);
        collector.RecordSourceLine("0x1234", "Contract.cs", 3);
        collector.RecordSourceLine("0x1234", "Contract.cs", 4);

        // Assert
        var report = collector.GetReport();
        report.Contracts[0].LineCoverage.Should().Be(20); // 4/20 = 20%
    }

    [Fact]
    public void Reset_ClearsAllData()
    {
        // Arrange
        var collector = new CoverageCollector();
        collector.SetTotalInstructions("0x1234", 100);
        collector.Start();
        collector.RecordInstruction("0x1234", 0, "OP1");

        // Act
        collector.Reset();

        // Assert
        var report = collector.GetReport();
        report.Contracts.Should().BeEmpty();
    }

    [Fact]
    public void GetReport_ReturnsGeneratedTimestamp()
    {
        // Arrange
        var collector = new CoverageCollector();
        var beforeTime = DateTime.UtcNow;

        // Act
        var report = collector.GetReport();
        var afterTime = DateTime.UtcNow;

        // Assert
        report.GeneratedAt.Should().BeOnOrAfter(beforeTime);
        report.GeneratedAt.Should().BeOnOrBefore(afterTime);
    }

    [Fact]
    public void MultipleContracts_TrackedSeparately()
    {
        // Arrange
        var collector = new CoverageCollector();
        collector.SetTotalInstructions("0x1111", 50);
        collector.SetTotalInstructions("0x2222", 100);
        collector.Start();

        // Act
        collector.RecordInstruction("0x1111", 0, "OP1");
        collector.RecordInstruction("0x2222", 0, "OP1");
        collector.RecordInstruction("0x2222", 1, "OP2");

        // Assert
        var report = collector.GetReport();
        report.Contracts.Should().HaveCount(2);
        report.Contracts.First(c => c.ContractHash == "0x1111").ExecutedInstructions.Should().Be(1);
        report.Contracts.First(c => c.ContractHash == "0x2222").ExecutedInstructions.Should().Be(2);
    }

    [Fact]
    public void OverallCoverage_AveragesAcrossContracts()
    {
        // Arrange
        var collector = new CoverageCollector();
        collector.SetTotalInstructions("0x1111", 10);
        collector.SetTotalInstructions("0x2222", 10);
        collector.Start();

        // Contract 1: 5/10 = 50%
        for (int i = 0; i < 5; i++)
            collector.RecordInstruction("0x1111", i, "OP");

        // Contract 2: 10/10 = 100%
        for (int i = 0; i < 10; i++)
            collector.RecordInstruction("0x2222", i, "OP");

        // Assert
        var report = collector.GetReport();
        report.OverallInstructionCoverage.Should().Be(75); // (50 + 100) / 2 = 75%
    }
}

public class CoverageReporterTests
{
    private CoverageReport CreateSampleReport()
    {
        var collector = new CoverageCollector();
        collector.SetTotalInstructions("0x1234567890abcdef", 100);
        collector.SetTotalSourceLines("0x1234567890abcdef", "Counter.cs", 50);
        collector.Start();

        for (int i = 0; i < 75; i++)
            collector.RecordInstruction("0x1234567890abcdef", i, "OP");

        for (int i = 1; i <= 40; i++)
            collector.RecordSourceLine("0x1234567890abcdef", "Counter.cs", i);

        var report = collector.GetReport();
        // Set contract name
        report.Contracts[0].ContractName = "Counter";
        return report;
    }

    [Fact]
    public void GenerateConsoleSummary_ContainsHeader()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var summary = CoverageReporter.GenerateConsoleSummary(report);

        // Assert
        summary.Should().Contain("COVERAGE REPORT");
    }

    [Fact]
    public void GenerateConsoleSummary_ContainsContractInfo()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var summary = CoverageReporter.GenerateConsoleSummary(report);

        // Assert
        summary.Should().Contain("Counter");
        summary.Should().Contain("Lines:");
        summary.Should().Contain("Instructions:");
    }

    [Fact]
    public void GenerateConsoleSummary_ContainsOverallCoverage()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var summary = CoverageReporter.GenerateConsoleSummary(report);

        // Assert
        summary.Should().Contain("Overall Coverage:");
    }

    [Fact]
    public void GenerateLcov_HasCorrectFormat()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var lcov = CoverageReporter.GenerateLcov(report);

        // Assert
        lcov.Should().Contain("TN:");
        lcov.Should().Contain("SF:Counter.cs");
        lcov.Should().Contain("DA:");
        lcov.Should().Contain("LF:");
        lcov.Should().Contain("LH:");
        lcov.Should().Contain("end_of_record");
    }

    [Fact]
    public void GenerateLcov_ContainsLineData()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var lcov = CoverageReporter.GenerateLcov(report);

        // Assert
        lcov.Should().Contain("DA:1,1");
        lcov.Should().Contain("LF:50");
        lcov.Should().Contain("LH:40");
    }

    [Fact]
    public void GenerateHtml_ContainsHtmlStructure()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = CoverageReporter.GenerateHtml(report);

        // Assert
        html.Should().Contain("<!DOCTYPE html>");
        html.Should().Contain("<html");
        html.Should().Contain("</html>");
        html.Should().Contain("<head>");
        html.Should().Contain("<body>");
    }

    [Fact]
    public void GenerateHtml_ContainsTitle()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = CoverageReporter.GenerateHtml(report);

        // Assert
        html.Should().Contain("<title>Neo Fairy Coverage Report</title>");
    }

    [Fact]
    public void GenerateHtml_ContainsContractData()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = CoverageReporter.GenerateHtml(report);

        // Assert
        html.Should().Contain("Counter");
        html.Should().Contain("Counter.cs");
    }

    [Fact]
    public void GenerateHtml_ContainsProgressBar()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var html = CoverageReporter.GenerateHtml(report);

        // Assert
        html.Should().Contain("progress-bar");
    }

    [Fact]
    public void GenerateJson_IsValidJsonStructure()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var json = CoverageReporter.GenerateJson(report);

        // Assert
        json.Should().Contain("\"generatedAt\":");
        json.Should().Contain("\"overallLineCoverage\":");
        json.Should().Contain("\"contracts\":");
    }

    [Fact]
    public void GenerateJson_ContainsContractData()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var json = CoverageReporter.GenerateJson(report);

        // Assert
        json.Should().Contain("\"hash\": \"0x1234567890abcdef\"");
        json.Should().Contain("\"name\": \"Counter\"");
        json.Should().Contain("\"files\":");
    }

    [Fact]
    public void WriteReports_CreatesAllFiles()
    {
        // Arrange
        var report = CreateSampleReport();
        var tempDir = Path.Combine(Path.GetTempPath(), $"fairy_coverage_test_{Guid.NewGuid():N}");

        try
        {
            // Act
            CoverageReporter.WriteReports(report, tempDir);

            // Assert
            File.Exists(Path.Combine(tempDir, "coverage.txt")).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "lcov.info")).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "coverage.html")).Should().BeTrue();
            File.Exists(Path.Combine(tempDir, "coverage.json")).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
