// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.Numerics;
using Neo.Fairy.Cli.Utilities;
using FluentAssertions;
using Xunit;

namespace Neo.Fairy.Cli.Tests.Utilities;

public class CliArgumentParserTests
{
    [Theory]
    [InlineData("0x0123456789abcdef0123456789abcdef01234567890123", true)]
    [InlineData("0xabcdef0123456789abcdef0123456789abcdef01", true)]
    [InlineData("0x1234", false)]
    [InlineData("1234567890abcdef1234567890abcdef12345678", false)]
    [InlineData("notahash", false)]
    [InlineData("0x", false)]
    public void LooksLikeHash_ShouldDetectHashFormats(string input, bool expected)
    {
        var result = CliArgumentParser.LooksLikeHash(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void ParseArgument_WithColonPrefix_ReturnsOriginalString()
    {
        var result = CliArgumentParser.ParseArgument("hash160:0xabc");
        result.Should().BeOfType<string>();
        result.Should().Be("hash160:0xabc");
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    public void ParseArgument_WithBoolean_ReturnsBoolean(string input, bool expected)
    {
        var result = CliArgumentParser.ParseArgument(input);
        result.Should().BeOfType<bool>();
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("0")]
    [InlineData("-456")]
    [InlineData("999999999999999999999999999999")]
    public void ParseArgument_WithNumber_ReturnsBigInteger(string input)
    {
        var result = CliArgumentParser.ParseArgument(input);
        result.Should().BeOfType<BigInteger>();
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("contract_name")]
    [InlineData("")]
    public void ParseArgument_WithString_ReturnsString(string input)
    {
        var result = CliArgumentParser.ParseArgument(input);
        result.Should().BeOfType<string>();
        result.Should().Be(input);
    }
}
