// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using FluentAssertions;
using Neo.Fairy.Core.CodeGen;
using Xunit;

namespace Neo.Fairy.Core.Tests.CodeGen;

public class ManifestParserTests
{
    private const string SampleManifest = """
    {
        "name": "TestContract",
        "groups": [],
        "supportedstandards": ["NEP-17"],
        "abi": {
            "methods": [
                {
                    "name": "transfer",
                    "parameters": [
                        { "name": "from", "type": "Hash160" },
                        { "name": "to", "type": "Hash160" },
                        { "name": "amount", "type": "Integer" },
                        { "name": "data", "type": "Any" }
                    ],
                    "returntype": "Boolean",
                    "offset": 0,
                    "safe": false
                },
                {
                    "name": "balanceOf",
                    "parameters": [
                        { "name": "account", "type": "Hash160" }
                    ],
                    "returntype": "Integer",
                    "offset": 100,
                    "safe": true
                },
                {
                    "name": "symbol",
                    "parameters": [],
                    "returntype": "String",
                    "offset": 200,
                    "safe": true
                },
                {
                    "name": "_deploy",
                    "parameters": [
                        { "name": "data", "type": "Any" },
                        { "name": "update", "type": "Boolean" }
                    ],
                    "returntype": "Void",
                    "offset": 300,
                    "safe": false
                }
            ],
            "events": [
                {
                    "name": "Transfer",
                    "parameters": [
                        { "name": "from", "type": "Hash160" },
                        { "name": "to", "type": "Hash160" },
                        { "name": "amount", "type": "Integer" }
                    ]
                }
            ]
        },
        "permissions": [
            { "contract": "*", "methods": "*" }
        ],
        "trusts": []
    }
    """;

    [Fact]
    public void Parse_ValidManifest_ReturnsContractManifest()
    {
        // Act
        var manifest = ManifestParser.Parse(SampleManifest);

        // Assert
        manifest.Should().NotBeNull();
        manifest.Name.Should().Be("TestContract");
    }

    [Fact]
    public void Parse_ExtractsSupportedStandards()
    {
        // Act
        var manifest = ManifestParser.Parse(SampleManifest);

        // Assert
        manifest.SupportedStandards.Should().Contain("NEP-17");
    }

    [Fact]
    public void Parse_ExtractsMethods()
    {
        // Act
        var manifest = ManifestParser.Parse(SampleManifest);

        // Assert
        manifest.Abi.Methods.Should().HaveCount(4);
    }

    [Fact]
    public void Parse_TransferMethod_HasCorrectParameters()
    {
        // Act
        var manifest = ManifestParser.Parse(SampleManifest);
        var transferMethod = manifest.Abi.Methods.First(m => m.Name == "transfer");

        // Assert
        transferMethod.Parameters.Should().HaveCount(4);
        transferMethod.Parameters[0].Name.Should().Be("from");
        transferMethod.Parameters[0].Type.Should().Be(ContractParameterType.Hash160);
        transferMethod.Parameters[1].Name.Should().Be("to");
        transferMethod.Parameters[2].Name.Should().Be("amount");
        transferMethod.Parameters[2].Type.Should().Be(ContractParameterType.Integer);
        transferMethod.ReturnType.Should().Be(ContractParameterType.Boolean);
        transferMethod.Safe.Should().BeFalse();
    }

    [Fact]
    public void Parse_SafeMethod_HasSafeFlag()
    {
        // Act
        var manifest = ManifestParser.Parse(SampleManifest);
        var balanceOfMethod = manifest.Abi.Methods.First(m => m.Name == "balanceOf");

        // Assert
        balanceOfMethod.Safe.Should().BeTrue();
    }

    [Fact]
    public void Parse_DeployMethod_IsInternal()
    {
        // Act
        var manifest = ManifestParser.Parse(SampleManifest);
        var deployMethod = manifest.Abi.Methods.First(m => m.Name == "_deploy");

        // Assert
        deployMethod.IsInternal.Should().BeTrue();
        deployMethod.IsDeploy.Should().BeTrue();
    }

    [Fact]
    public void Parse_MethodWithNoParameters_HasEmptyParameterList()
    {
        // Act
        var manifest = ManifestParser.Parse(SampleManifest);
        var symbolMethod = manifest.Abi.Methods.First(m => m.Name == "symbol");

        // Assert
        symbolMethod.Parameters.Should().BeEmpty();
        symbolMethod.ReturnType.Should().Be(ContractParameterType.String);
    }

    [Fact]
    public void Parse_ExtractsEvents()
    {
        // Act
        var manifest = ManifestParser.Parse(SampleManifest);

        // Assert
        manifest.Abi.Events.Should().HaveCount(1);
        var transferEvent = manifest.Abi.Events.First();
        transferEvent.Name.Should().Be("Transfer");
        transferEvent.Parameters.Should().HaveCount(3);
    }

    [Fact]
    public void Parse_EventParameters_HaveCorrectTypes()
    {
        // Act
        var manifest = ManifestParser.Parse(SampleManifest);
        var transferEvent = manifest.Abi.Events.First();

        // Assert
        transferEvent.Parameters[0].Name.Should().Be("from");
        transferEvent.Parameters[0].Type.Should().Be(ContractParameterType.Hash160);
        transferEvent.Parameters[2].Name.Should().Be("amount");
        transferEvent.Parameters[2].Type.Should().Be(ContractParameterType.Integer);
    }

    [Fact]
    public void Parse_Permissions_AreExtracted()
    {
        // Act
        var manifest = ManifestParser.Parse(SampleManifest);

        // Assert
        manifest.Permissions.Should().HaveCount(1);
        manifest.Permissions[0].Contract.Should().Be("*");
    }

    [Theory]
    [InlineData("Boolean", ContractParameterType.Boolean)]
    [InlineData("Integer", ContractParameterType.Integer)]
    [InlineData("ByteArray", ContractParameterType.ByteArray)]
    [InlineData("String", ContractParameterType.String)]
    [InlineData("Hash160", ContractParameterType.Hash160)]
    [InlineData("Hash256", ContractParameterType.Hash256)]
    [InlineData("PublicKey", ContractParameterType.PublicKey)]
    [InlineData("Array", ContractParameterType.Array)]
    [InlineData("Map", ContractParameterType.Map)]
    [InlineData("Void", ContractParameterType.Void)]
    [InlineData("Any", ContractParameterType.Any)]
    public void Parse_ParameterTypes_MapCorrectly(string typeString, ContractParameterType expectedType)
    {
        // Arrange
        var manifest = $$"""
        {
            "name": "Test",
            "groups": [],
            "supportedstandards": [],
            "abi": {
                "methods": [
                    {
                        "name": "test",
                        "parameters": [{ "name": "param", "type": "{{typeString}}" }],
                        "returntype": "Void",
                        "offset": 0,
                        "safe": false
                    }
                ],
                "events": []
            },
            "permissions": [],
            "trusts": []
        }
        """;

        // Act
        var parsed = ManifestParser.Parse(manifest);
        var method = parsed.Abi.Methods.First();

        // Assert
        method.Parameters[0].Type.Should().Be(expectedType);
    }

    [Fact]
    public void Parse_MinimalManifest_ReturnsValidResult()
    {
        // Arrange
        var minimalManifest = """
        {
            "name": "Minimal",
            "groups": [],
            "supportedstandards": [],
            "abi": {
                "methods": [],
                "events": []
            },
            "permissions": [],
            "trusts": []
        }
        """;

        // Act
        var manifest = ManifestParser.Parse(minimalManifest);

        // Assert
        manifest.Name.Should().Be("Minimal");
        manifest.Abi.Methods.Should().BeEmpty();
        manifest.Abi.Events.Should().BeEmpty();
    }
}
