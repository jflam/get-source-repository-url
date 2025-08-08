# get-source-repository-url Tool Implementation Plan

## Overview

A Unix-style command-line tool that extracts repository information from .NET assembly metadata. Designed to help coding agents and developers locate source code corresponding to compiled assemblies.

## Tool Purpose

Modern .NET assemblies (SDK 8+) contain embedded metadata about their source repository:
- `AssemblyInformationalVersionAttribute`: Contains version + commit SHA (e.g., "9.3.0+f76a033601ade4a17a422d5d1e4d004ab85e5179")
- `AssemblyMetadataAttribute`: Contains key-value pairs like ("RepositoryUrl", "https://github.com/dotnet/aspire")

This tool extracts this information in a parseable format for use with standard Unix tools.

## Command Line Interface

### Basic Usage
```bash
get-source-repository-url <assembly-path>
```

### Options
```bash
get-source-repository-url [OPTIONS] <assembly-path>

OPTIONS:
  --json              Output in JSON format instead of space-separated
  --help, -h          Show detailed help information
  --version, -v       Show version information
  --verbose           Show diagnostic information to stderr
  --quiet             Suppress all output except results
```

### Output Formats

**Default (space-separated):**
```
https://github.com/dotnet/aspire f76a033601ade4a17a422d5d1e4d004ab85e5179
```

**JSON format (--json):**
```json
{"repository_url":"https://github.com/dotnet/aspire","commit_sha":"f76a033601ade4a17a422d5d1e4d004ab85e5179"}
```

## Detailed Help Text

```
get-source-repository-url - Extract repository information from .NET assemblies

SYNOPSIS
    get-source-repository-url [OPTIONS] <assembly-path>

DESCRIPTION
    Extracts source repository URL and commit SHA from .NET assembly metadata.
    Modern .NET assemblies (SDK 8+) embed build-time repository information that
    can be used to locate the exact source code used to build the assembly.

    This tool reads the following assembly attributes:
    • AssemblyInformationalVersionAttribute - Contains commit SHA after '+'
    • AssemblyMetadataAttribute("RepositoryUrl") - Contains repository URL

    Designed for coding agents and automated tools that need to analyze source
    code corresponding to compiled assemblies.

USAGE EXAMPLES
    # Extract repository info
    get-source-repository-url MyApp.dll
    → https://github.com/myorg/myapp abc123def456...

    # Use with git to clone and checkout exact source
    repo_url=$(get-source-repository-url MyApp.dll | cut -d' ' -f1)
    commit_sha=$(get-source-repository-url MyApp.dll | cut -d' ' -f2)
    git clone "$repo_url" /tmp/source
    cd /tmp/source && git checkout "$commit_sha"

    # JSON output for programmatic use
    get-source-repository-url --json MyApp.dll | jq -r '.repository_url'

    # Batch process multiple assemblies
    find . -name "*.dll" -exec get-source-repository-url {} \;

OPTIONS
    --json              Output as JSON instead of space-separated values
    --verbose           Show diagnostic information to stderr
    --quiet             Suppress all output except results and errors
    --help, -h          Show this help message
    --version, -v       Show version information

EXIT CODES
    0    Success - repository information extracted
    1    File not found or not a valid .NET assembly
    2    Assembly found but missing repository metadata
    3    Assembly contains malformed repository metadata
    4    Invalid command line arguments

REQUIREMENTS
    • .NET 8+ runtime
    • Assembly must contain embedded repository metadata
    • Assembly must be accessible and valid PE file

NOTES
    This tool only reads metadata - it does not load or execute the assembly.
    Compatible with assemblies targeting any .NET version if built with SDK 8+.

    For assemblies without embedded metadata, consider using symbol servers
    or other source indexing mechanisms.
```

## Implementation Details

### Core Components

1. **Assembly Metadata Reader**
   - Use `System.Reflection.Metadata` with `PEReader`
   - Parse `AssemblyInformationalVersionAttribute` for commit SHA
   - Parse `AssemblyMetadataAttribute` for repository URL
   - Handle both modern and legacy metadata formats

2. **CLI Argument Parsing**
   - Single required positional argument (assembly path)
   - Optional flags for output format and verbosity
   - Comprehensive help system

3. **Output Formatting**
   - Default: space-separated values for easy shell parsing
   - JSON: structured output for programmatic consumption
   - Consistent error messages to stderr

### Technical Implementation

**Language:** C# (.NET 8+)
**Dependencies:** 
- `System.Reflection.Metadata` (included in runtime)
- `System.CommandLine` for argument parsing

**Project Structure:**
```
src/
├── GetSourceRepositoryUrl/
│   ├── Program.cs              # Entry point and CLI setup
│   ├── AssemblyMetadataReader.cs  # Core extraction logic
│   ├── OutputFormatter.cs      # Format results
│   └── GetSourceRepositoryUrl.csproj
├── Tests/
│   └── GetSourceRepositoryUrl.Tests/
└── TestData/
    ├── WithMetadata.dll         # Test assembly with metadata
    ├── WithoutMetadata.dll      # Test assembly without metadata
    └── MalformedMetadata.dll    # Test assembly with bad metadata
```

### Error Handling

- **File Access**: Check file existence and readability
- **PE Validation**: Verify file is valid .NET assembly
- **Metadata Parsing**: Handle missing or malformed attributes
- **Format Validation**: Validate commit SHA format (40-char hex)
- **URL Validation**: Basic repository URL format checking

### Exit Codes

| Code | Meaning | Example |
|------|---------|---------|
| 0 | Success | Repository info extracted successfully |
| 1 | File error | File not found, not readable, or not a PE file |
| 2 | Missing metadata | Valid assembly but no repository information |
| 3 | Malformed metadata | Repository info present but invalid format |
| 4 | Invalid arguments | Bad command line usage |

### Testing Strategy

1. **Unit Tests**: Test metadata extraction with various assembly formats
2. **Integration Tests**: Test CLI with real assemblies
3. **Error Cases**: Test all failure modes and exit codes
4. **Output Formats**: Verify both default and JSON output
5. **Edge Cases**: Empty files, corrupted assemblies, missing attributes

### Build and Distribution

- **Build**: Standard `dotnet build`
- **Package**: Single-file executable with `dotnet publish`
- **Distribution**: GitHub releases with binaries for major platforms
- **Installation**: Optional NuGet global tool package

### Future Enhancements

- Support for reading from NuGet packages (.nupkg files)
- Batch processing mode for multiple assemblies
- Integration with symbol servers for assemblies without metadata
- Plugin system for custom metadata sources

## Sample Test DLL for Development

For development and testing, we can use a well-known assembly with embedded metadata:

### ASP.NET Core Assembly Example
**File**: `Microsoft.AspNetCore.App.dll` (from .NET 8+ installation)
**Location**: Usually found in `%ProgramFiles%\dotnet\shared\Microsoft.AspNetCore.App\<version>\`

**Expected Metadata Attributes:**
```csharp
[assembly: AssemblyInformationalVersion("8.0.0+5535e31a712343a63f5d7d796cd874e563e5ac14")]
[assembly: AssemblyMetadata("RepositoryUrl", "https://github.com/dotnet/aspnetcore")]
[assembly: AssemblyMetadata("CommitHash", "5535e31a712343a63f5d7d796cd874e563e5ac14")]
[assembly: AssemblyMetadata("SourceCommitUrl", "https://github.com/dotnet/aspnetcore/tree/5535e31a712343a63f5d7d796cd874e563e5ac14")]
```

**Expected Tool Output:**
```bash
$ get-source-repository-url Microsoft.AspNetCore.App.dll
https://github.com/dotnet/aspnetcore 5535e31a712343a63f5d7d796cd874e563e5ac14

$ get-source-repository-url --json Microsoft.AspNetCore.App.dll
{"repository_url":"https://github.com/dotnet/aspnetcore","commit_sha":"5535e31a712343a63f5d7d796cd874e563e5ac14"}
```

### Alternative Test Assemblies

**System.Text.Json.dll** (from .NET 8+ runtime):
- Repository: `https://github.com/dotnet/runtime`
- Contains similar metadata structure
- Easily accessible on any .NET 8+ system

**Microsoft.Extensions.Hosting.dll**:
- Repository: `https://github.com/dotnet/runtime`
- Part of extensions framework
- Good for testing different metadata patterns

### Smoke Test Validation

The tool should successfully extract:
1. **Repository URL** from `AssemblyMetadata("RepositoryUrl")` or inferred from commit URL
2. **Commit SHA** from `AssemblyInformationalVersion` (after '+') or `AssemblyMetadata("CommitHash")`
3. **Format correctly** as space-separated values or JSON
4. **Handle gracefully** when metadata is missing or malformed

### Creating Test Data

For comprehensive testing, we can create minimal test assemblies:

```csharp
// TestAssemblyWithMetadata - compile with .NET 8+ SDK in git repo
[assembly: AssemblyMetadata("RepositoryUrl", "https://github.com/test/repo")]
[assembly: AssemblyInformationalVersion("1.0.0+abcd1234567890abcd1234567890abcd12345678")]

// TestAssemblyWithoutMetadata - compile without git metadata
[assembly: AssemblyVersion("1.0.0.0")]
```

## Usage Scenarios for Coding Agents

1. **Source Code Analysis**: Agent needs to analyze source that corresponds to a compiled library
2. **Dependency Tracking**: Trace exact versions of dependencies in a deployed application
3. **Security Auditing**: Verify source code matches deployed assemblies
4. **Debugging**: Access source code for assemblies without local source
5. **Documentation**: Generate documentation linking to exact source versions
6. **Code Review**: Review source changes between assembly versions

This tool enables agents to bridge the gap between compiled code and source repositories, making automated code analysis more effective and accurate.