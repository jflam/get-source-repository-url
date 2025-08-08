using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace GetSourceRepositoryUrl;

public class AssemblyMetadataReader
{
    public record MetadataResult(string? RepositoryUrl, string? CommitSha, bool Success, string? ErrorMessage);

    public static MetadataResult ExtractMetadata(string assemblyPath)
    {
        try
        {
            if (!File.Exists(assemblyPath))
            {
                return new MetadataResult(null, null, false, "File not found");
            }

            using var fileStream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var peReader = new PEReader(fileStream);

            if (!peReader.HasMetadata)
            {
                return new MetadataResult(null, null, false, "File does not contain .NET metadata");
            }

            var metadataReader = peReader.GetMetadataReader();
            
            if (!metadataReader.IsAssembly)
            {
                return new MetadataResult(null, null, false, "File is not a .NET assembly");
            }

            var repositoryUrl = ExtractRepositoryUrl(metadataReader);
            var commitSha = ExtractCommitSha(metadataReader);

            if (repositoryUrl == null && commitSha == null)
            {
                return new MetadataResult(null, null, false, "No repository metadata found");
            }

            if (repositoryUrl == null || commitSha == null)
            {
                return new MetadataResult(repositoryUrl, commitSha, false, "Incomplete repository metadata");
            }

            return new MetadataResult(repositoryUrl, commitSha, true, null);
        }
        catch (BadImageFormatException)
        {
            return new MetadataResult(null, null, false, "Invalid PE file format");
        }
        catch (Exception ex)
        {
            return new MetadataResult(null, null, false, $"Error reading assembly: {ex.Message}");
        }
    }

    private static string? ExtractRepositoryUrl(MetadataReader reader)
    {
        // First try to get from AssemblyMetadata attributes
        foreach (var attributeHandle in reader.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = reader.GetCustomAttribute(attributeHandle);
            var attributeType = GetAttributeTypeName(reader, attribute);

            if (attributeType == "System.Reflection.AssemblyMetadataAttribute")
            {
                var (key, value) = DecodeAssemblyMetadataAttribute(reader, attribute);
                if (key == "RepositoryUrl" && !string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
        }

        // Fallback: try to extract from SourceCommitUrl if available
        foreach (var attributeHandle in reader.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = reader.GetCustomAttribute(attributeHandle);
            var attributeType = GetAttributeTypeName(reader, attribute);

            if (attributeType == "System.Reflection.AssemblyMetadataAttribute")
            {
                var (key, value) = DecodeAssemblyMetadataAttribute(reader, attribute);
                if (key == "SourceCommitUrl" && !string.IsNullOrEmpty(value))
                {
                    // Extract repo URL from commit URL (e.g., https://github.com/org/repo/tree/commit -> https://github.com/org/repo)
                    return ExtractRepoUrlFromCommitUrl(value);
                }
            }
        }

        return null;
    }

    private static string? ExtractCommitSha(MetadataReader reader)
    {
        // First try to get from AssemblyMetadata CommitHash attribute
        foreach (var attributeHandle in reader.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = reader.GetCustomAttribute(attributeHandle);
            var attributeType = GetAttributeTypeName(reader, attribute);

            if (attributeType == "System.Reflection.AssemblyMetadataAttribute")
            {
                var (key, value) = DecodeAssemblyMetadataAttribute(reader, attribute);
                if (key == "CommitHash" && !string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
        }

        // Fallback: try to extract from AssemblyInformationalVersion
        foreach (var attributeHandle in reader.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = reader.GetCustomAttribute(attributeHandle);
            var attributeType = GetAttributeTypeName(reader, attribute);

            if (attributeType == "System.Reflection.AssemblyInformationalVersionAttribute")
            {
                var version = DecodeStringAttribute(reader, attribute);
                if (!string.IsNullOrEmpty(version))
                {
                    // Extract commit hash from version string (e.g., "1.0.0+abc123def456...")
                    var plusIndex = version.IndexOf('+');
                    if (plusIndex >= 0 && plusIndex + 1 < version.Length)
                    {
                        var hash = version.Substring(plusIndex + 1);
                        // Validate it looks like a commit hash (40 hex chars)
                        if (IsValidCommitHash(hash))
                        {
                            return hash;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static string GetAttributeTypeName(MetadataReader reader, CustomAttribute attribute)
    {
        var ctorHandle = attribute.Constructor;
        
        if (ctorHandle.Kind == HandleKind.MemberReference)
        {
            var memberRef = reader.GetMemberReference((MemberReferenceHandle)ctorHandle);
            var typeRef = reader.GetTypeReference((TypeReferenceHandle)memberRef.Parent);
            var typeName = reader.GetString(typeRef.Name);
            var typeNamespace = reader.GetString(typeRef.Namespace);
            return $"{typeNamespace}.{typeName}";
        }
        
        return string.Empty;
    }

    private static (string key, string value) DecodeAssemblyMetadataAttribute(MetadataReader reader, CustomAttribute attribute)
    {
        var valueBlob = reader.GetBlobReader(attribute.Value);
        
        // Skip the prolog (2 bytes)
        valueBlob.ReadUInt16();
        
        // Read key (first string argument)
        var key = valueBlob.ReadSerializedString() ?? string.Empty;
        
        // Read value (second string argument)
        var value = valueBlob.ReadSerializedString() ?? string.Empty;
        
        return (key, value);
    }

    private static string? DecodeStringAttribute(MetadataReader reader, CustomAttribute attribute)
    {
        var valueBlob = reader.GetBlobReader(attribute.Value);
        
        // Skip the prolog (2 bytes)
        valueBlob.ReadUInt16();
        
        // Read the string argument
        return valueBlob.ReadSerializedString();
    }

    private static string? ExtractRepoUrlFromCommitUrl(string commitUrl)
    {
        // Handle GitHub URLs like: https://github.com/org/repo/tree/commit
        if (commitUrl.Contains("github.com") && commitUrl.Contains("/tree/"))
        {
            var treeIndex = commitUrl.IndexOf("/tree/");
            if (treeIndex > 0)
            {
                return commitUrl.Substring(0, treeIndex);
            }
        }
        
        // Handle other common patterns as needed
        return null;
    }

    private static bool IsValidCommitHash(string hash)
    {
        // Basic validation: should be 40 hexadecimal characters
        if (hash.Length != 40)
            return false;
            
        return hash.All(c => char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
    }
}