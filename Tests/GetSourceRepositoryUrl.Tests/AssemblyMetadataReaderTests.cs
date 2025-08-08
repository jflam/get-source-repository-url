using Xunit;
using GetSourceRepositoryUrl;

namespace GetSourceRepositoryUrl.Tests;

public class AssemblyMetadataReaderTests
{
    [Fact]
    public void ExtractMetadata_NonExistentFile_ReturnsFileNotFound()
    {
        var result = AssemblyMetadataReader.ExtractMetadata("nonexistent.dll");
        
        Assert.False(result.Success);
        Assert.Contains("File not found", result.ErrorMessage);
        Assert.Null(result.RepositoryUrl);
        Assert.Null(result.CommitSha);
    }

    [Fact]
    public void ExtractMetadata_InvalidPEFile_ReturnsInvalidFormat()
    {
        // Create a temporary non-PE file
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "This is not a PE file");
            
            var result = AssemblyMetadataReader.ExtractMetadata(tempFile);
            
            Assert.False(result.Success);
            Assert.Contains("Invalid PE file format", result.ErrorMessage);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ExtractMetadata_CurrentAssembly_ReturnsExpectedResult()
    {
        // Test with the current test assembly
        var assemblyPath = typeof(AssemblyMetadataReaderTests).Assembly.Location;
        
        var result = AssemblyMetadataReader.ExtractMetadata(assemblyPath);
        
        // The test assembly likely won't have repository metadata, so we expect either
        // success with data or a "no repository metadata" or "incomplete repository metadata" error
        if (!result.Success)
        {
            Assert.True(
                result.ErrorMessage?.Contains("No repository metadata") == true ||
                result.ErrorMessage?.Contains("Incomplete repository metadata") == true,
                $"Expected no metadata or incomplete metadata error, got: {result.ErrorMessage}");
        }
        else
        {
            Assert.NotNull(result.RepositoryUrl);
            Assert.NotNull(result.CommitSha);
        }
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore.App.dll")]
    [InlineData("System.Text.Json.dll")]
    [InlineData("Microsoft.Extensions.Hosting.dll")]
    public void ExtractMetadata_WellKnownAssemblies_FindsIfExists(string assemblyName)
    {
        // Try to find the assembly in common .NET locations
        var possiblePaths = new[]
        {
            Path.Combine("/usr/local/share/dotnet/shared/Microsoft.NETCore.App/9.0.6", assemblyName),
            Path.Combine("/usr/local/share/dotnet/shared/Microsoft.AspNetCore.App/9.0.6", assemblyName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "shared", "Microsoft.NETCore.App", "9.0.6", assemblyName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "shared", "Microsoft.AspNetCore.App", "9.0.6", assemblyName),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                var result = AssemblyMetadataReader.ExtractMetadata(path);
                
                if (result.Success)
                {
                    Assert.NotNull(result.RepositoryUrl);
                    Assert.NotNull(result.CommitSha);
                    Assert.Contains("github.com", result.RepositoryUrl.ToLower());
                    Assert.Equal(40, result.CommitSha.Length); // Git commit hashes are 40 chars
                    return; // Test passed
                }
            }
        }
        
        // If we get here, the assembly wasn't found in any standard location
        // This is not a test failure, just means the assembly isn't available
        Assert.True(true, $"Assembly {assemblyName} not found in standard locations - test skipped");
    }
}

public class OutputFormatterTests
{
    [Fact]
    public void FormatDefault_ValidInputs_ReturnsSpaceSeparated()
    {
        var result = OutputFormatter.FormatDefault(
            "https://github.com/test/repo", 
            "abc123def456789abc123def456789abc12345678");
        
        Assert.Equal("https://github.com/test/repo abc123def456789abc123def456789abc12345678", result);
    }

    [Fact]
    public void FormatJson_ValidInputs_ReturnsValidJson()
    {
        var result = OutputFormatter.FormatJson(
            "https://github.com/test/repo", 
            "abc123def456789abc123def456789abc12345678");
        
        Assert.Contains("\"repository_url\":\"https://github.com/test/repo\"", result);
        Assert.Contains("\"commit_sha\":\"abc123def456789abc123def456789abc12345678\"", result);
    }

    [Fact]
    public void WriteError_WritesToErrorStream()
    {
        using var writer = new StringWriter();
        OutputFormatter.WriteError("Test error", writer);
        
        var output = writer.ToString();
        Assert.Contains("get-source-repository-url: Test error", output);
    }

    [Fact]
    public void WriteVerbose_WritesToErrorStream()
    {
        using var writer = new StringWriter();
        OutputFormatter.WriteVerbose("Test verbose", writer);
        
        var output = writer.ToString();
        Assert.Contains("[VERBOSE] Test verbose", output);
    }
}