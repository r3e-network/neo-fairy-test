// Copyright (C) 2015-2025 The Neo Project.
// Licensed under the MIT License.

using System.Text.Json;

namespace Neo.Fairy.Core.CodeGen;

/// <summary>
/// Parses Neo contract manifest to extract method and event information.
/// </summary>
public static class ManifestParser
{
    /// <summary>
    /// Parses a manifest JSON string and extracts contract metadata.
    /// </summary>
    public static ContractManifest Parse(string manifestJson)
    {
        using var doc = JsonDocument.Parse(manifestJson);
        var root = doc.RootElement;

        var manifest = new ContractManifest
        {
            Name = root.GetProperty("name").GetString() ?? "Unknown",
            Groups = ParseGroups(root),
            SupportedStandards = ParseSupportedStandards(root),
            Abi = ParseAbi(root),
            Permissions = ParsePermissions(root),
            Trusts = ParseTrusts(root)
        };

        return manifest;
    }

    private static List<ContractGroup> ParseGroups(JsonElement root)
    {
        var groups = new List<ContractGroup>();
        if (root.TryGetProperty("groups", out var groupsElement))
        {
            foreach (var group in groupsElement.EnumerateArray())
            {
                groups.Add(new ContractGroup
                {
                    PubKey = group.GetProperty("pubkey").GetString() ?? "",
                    Signature = group.GetProperty("signature").GetString() ?? ""
                });
            }
        }
        return groups;
    }

    private static List<string> ParseSupportedStandards(JsonElement root)
    {
        var standards = new List<string>();
        if (root.TryGetProperty("supportedstandards", out var standardsElement))
        {
            foreach (var standard in standardsElement.EnumerateArray())
            {
                standards.Add(standard.GetString() ?? "");
            }
        }
        return standards;
    }

    private static ContractAbi ParseAbi(JsonElement root)
    {
        var abi = new ContractAbi();

        if (root.TryGetProperty("abi", out var abiElement))
        {
            // Parse methods
            if (abiElement.TryGetProperty("methods", out var methodsElement))
            {
                foreach (var method in methodsElement.EnumerateArray())
                {
                    abi.Methods.Add(ParseMethod(method));
                }
            }

            // Parse events
            if (abiElement.TryGetProperty("events", out var eventsElement))
            {
                foreach (var evt in eventsElement.EnumerateArray())
                {
                    abi.Events.Add(ParseEvent(evt));
                }
            }
        }

        return abi;
    }

    private static ContractMethod ParseMethod(JsonElement method)
    {
        var contractMethod = new ContractMethod
        {
            Name = method.GetProperty("name").GetString() ?? "",
            Safe = method.TryGetProperty("safe", out var safe) && safe.GetBoolean(),
            ReturnType = ParseParameterType(method.GetProperty("returntype"))
        };

        if (method.TryGetProperty("offset", out var offset))
        {
            contractMethod.Offset = offset.GetInt32();
        }

        if (method.TryGetProperty("parameters", out var parameters))
        {
            foreach (var param in parameters.EnumerateArray())
            {
                contractMethod.Parameters.Add(ParseParameter(param));
            }
        }

        return contractMethod;
    }

    private static ContractEvent ParseEvent(JsonElement evt)
    {
        var contractEvent = new ContractEvent
        {
            Name = evt.GetProperty("name").GetString() ?? ""
        };

        if (evt.TryGetProperty("parameters", out var parameters))
        {
            foreach (var param in parameters.EnumerateArray())
            {
                contractEvent.Parameters.Add(ParseParameter(param));
            }
        }

        return contractEvent;
    }

    private static ContractParameter ParseParameter(JsonElement param)
    {
        return new ContractParameter
        {
            Name = param.GetProperty("name").GetString() ?? "",
            Type = ParseParameterType(param.GetProperty("type"))
        };
    }

    private static ContractParameterType ParseParameterType(JsonElement typeElement)
    {
        var typeStr = typeElement.GetString() ?? "Any";
        return typeStr.ToLowerInvariant() switch
        {
            "any" => ContractParameterType.Any,
            "boolean" or "bool" => ContractParameterType.Boolean,
            "integer" => ContractParameterType.Integer,
            "bytearray" => ContractParameterType.ByteArray,
            "string" => ContractParameterType.String,
            "hash160" => ContractParameterType.Hash160,
            "hash256" => ContractParameterType.Hash256,
            "publickey" => ContractParameterType.PublicKey,
            "signature" => ContractParameterType.Signature,
            "array" => ContractParameterType.Array,
            "map" => ContractParameterType.Map,
            "interopinterface" => ContractParameterType.InteropInterface,
            "void" => ContractParameterType.Void,
            _ => ContractParameterType.Any
        };
    }

    private static List<ContractPermission> ParsePermissions(JsonElement root)
    {
        var permissions = new List<ContractPermission>();
        if (root.TryGetProperty("permissions", out var permsElement))
        {
            foreach (var perm in permsElement.EnumerateArray())
            {
                var permission = new ContractPermission();

                if (perm.TryGetProperty("contract", out var contract))
                {
                    permission.Contract = contract.GetString() ?? "*";
                }

                if (perm.TryGetProperty("methods", out var methods))
                {
                    if (methods.ValueKind == JsonValueKind.String && methods.GetString() == "*")
                    {
                        permission.Methods = new List<string> { "*" };
                    }
                    else if (methods.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var m in methods.EnumerateArray())
                        {
                            permission.Methods.Add(m.GetString() ?? "");
                        }
                    }
                }

                permissions.Add(permission);
            }
        }
        return permissions;
    }

    private static List<string> ParseTrusts(JsonElement root)
    {
        var trusts = new List<string>();
        if (root.TryGetProperty("trusts", out var trustsElement))
        {
            if (trustsElement.ValueKind == JsonValueKind.String && trustsElement.GetString() == "*")
            {
                trusts.Add("*");
            }
            else if (trustsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var trust in trustsElement.EnumerateArray())
                {
                    trusts.Add(trust.GetString() ?? "");
                }
            }
        }
        return trusts;
    }
}

/// <summary>
/// Represents a parsed contract manifest.
/// </summary>
public sealed class ContractManifest
{
    public required string Name { get; init; }
    public List<ContractGroup> Groups { get; init; } = new();
    public List<string> SupportedStandards { get; init; } = new();
    public ContractAbi Abi { get; init; } = new();
    public List<ContractPermission> Permissions { get; init; } = new();
    public List<string> Trusts { get; init; } = new();
}

/// <summary>
/// Contract group information.
/// </summary>
public sealed class ContractGroup
{
    public required string PubKey { get; init; }
    public required string Signature { get; init; }
}

/// <summary>
/// Contract ABI containing methods and events.
/// </summary>
public sealed class ContractAbi
{
    public List<ContractMethod> Methods { get; } = new();
    public List<ContractEvent> Events { get; } = new();
}

/// <summary>
/// Contract method definition.
/// </summary>
public sealed class ContractMethod
{
    public required string Name { get; init; }
    public int Offset { get; set; }
    public bool Safe { get; init; }
    public List<ContractParameter> Parameters { get; } = new();
    public required ContractParameterType ReturnType { get; init; }

    /// <summary>
    /// Gets whether this is a deploy method.
    /// </summary>
    public bool IsDeploy => Name == "_deploy";

    /// <summary>
    /// Gets whether this is an internal method (starts with _).
    /// </summary>
    public bool IsInternal => Name.StartsWith("_");
}

/// <summary>
/// Contract event definition.
/// </summary>
public sealed class ContractEvent
{
    public required string Name { get; init; }
    public List<ContractParameter> Parameters { get; } = new();
}

/// <summary>
/// Contract parameter definition.
/// </summary>
public sealed class ContractParameter
{
    public required string Name { get; init; }
    public required ContractParameterType Type { get; init; }
}

/// <summary>
/// Contract permission definition.
/// </summary>
public sealed class ContractPermission
{
    public string Contract { get; set; } = "*";
    public List<string> Methods { get; set; } = new();
}

/// <summary>
/// Neo contract parameter types.
/// </summary>
public enum ContractParameterType
{
    Any,
    Boolean,
    Integer,
    ByteArray,
    String,
    Hash160,
    Hash256,
    PublicKey,
    Signature,
    Array,
    Map,
    InteropInterface,
    Void
}
