# get-source-repository-url

[![.NET](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)]()

A Unix-style command-line tool that extracts repository URL and commit SHA from .NET assembly metadata. Designed specifically for coding agents and automated tools that need to locate source code corresponding to compiled assemblies.

## 🎯 Purpose

Modern .NET assemblies (SDK 8+) embed build-time repository information in their metadata. This tool extracts that information in a parseable format, enabling automated workflows to:

- **Locate exact source code** for any compiled assembly
- **Enable reproducible builds** and security auditing  
- **Support coding agents** in analyzing corresponding source code
- **Bridge the gap** between compiled binaries and source repositories

## 🚀 Quick Start

```bash
# Extract repository information
get-source-repository-url MyApp.dll
# Output: https://github.com/myorg/myapp abc123def456789abc123def456789abc12345678

# Clone and checkout exact source
repo_url=$(get-source-repository-url MyApp.dll | cut -d' ' -f1)
commit_sha=$(get-source-repository-url MyApp.dll | cut -d' ' -f2)
git clone "$repo_url" /tmp/source
cd /tmp/source && git checkout "$commit_sha"

# JSON output for programmatic use
get-source-repository-url --json MyApp.dll | jq -r '.repository_url'
```

## 📦 Installation

### Binary Releases
Download the latest binary from the [Releases](https://github.com/jflam/get-source-repository-url/releases) page.

### Build from Source
```bash
git clone https://github.com/jflam/get-source-repository-url.git
cd get-source-repository-url
dotnet publish src/GetSourceRepositoryUrl -c Release -o bin
./bin/get-source-repository-url --help
```

### Requirements
- **.NET 9.0 Runtime** (or later)
- **Compatible with** assemblies targeting any .NET version if built with SDK 8+

## 🔍 How It Works

The tool reads embedded metadata from .NET PE files without loading or executing the assembly:

### Assembly Attributes Parsed

**AssemblyInformationalVersionAttribute**
```csharp
[assembly: AssemblyInformationalVersion("8.0.0+5535e31a712343a63f5d7d796cd874e563e5ac14")]
//                                              ↑ Commit SHA extracted from here
```

**AssemblyMetadataAttribute**
```csharp
[assembly: AssemblyMetadata("RepositoryUrl", "https://github.com/dotnet/aspnetcore")]
[assembly: AssemblyMetadata("CommitHash", "5535e31a712343a63f5d7d796cd874e563e5ac14")]
```

### Technical Implementation
- Uses `System.Reflection.Metadata` for high-performance PE parsing
- Handles multiple metadata formats and fallback strategies
- Validates commit hash format (40-character hexadecimal)
- Works with assemblies from any .NET version built with modern SDK

## 📖 Usage

### Command Line Interface

```
get-source-repository-url <assembly-path> [options]

Arguments:
  <assembly-path>    Path to the .NET assembly file

Options:
  --json            Output in JSON format instead of space-separated
  --verbose         Show diagnostic information to stderr  
  --quiet           Suppress all output except results and errors
  --help, -h        Show help and usage information
  --version, -v     Show version information
```

### Output Formats

**Default (Space-separated)**
```bash
$ get-source-repository-url Microsoft.AspNetCore.App.dll
https://github.com/dotnet/aspnetcore 5535e31a712343a63f5d7d796cd874e563e5ac14
```

**JSON Format**
```bash
$ get-source-repository-url --json Microsoft.AspNetCore.App.dll
{"repository_url":"https://github.com/dotnet/aspnetcore","commit_sha":"5535e31a712343a63f5d7d796cd874e563e5ac14"}
```

### Exit Codes

| Code | Meaning | Description |
|------|---------|-------------|
| **0** | Success | Repository information extracted successfully |
| **1** | File Error | File not found, not readable, or invalid PE file |
| **2** | Missing Metadata | Valid assembly but no repository information found |
| **3** | Malformed Metadata | Repository info present but invalid format |
| **4** | Invalid Arguments | Bad command line usage |

## 🛠️ Usage Examples

### Shell Scripting
```bash
# Process multiple assemblies
find . -name "*.dll" -exec get-source-repository-url {} \;

# Extract specific field
repo_url=$(get-source-repository-url app.dll | cut -d' ' -f1)
commit_sha=$(get-source-repository-url app.dll | cut -d' ' -f2)

# Conditional processing
if get-source-repository-url app.dll >/dev/null 2>&1; then
    echo "Repository metadata found"
else
    echo "No repository information available"
fi
```

### JSON Processing with jq
```bash
# Extract repository URL
get-source-repository-url --json app.dll | jq -r '.repository_url'

# Batch process with JSON
find . -name "*.dll" | while read dll; do
    result=$(get-source-repository-url --json "$dll" 2>/dev/null || echo '{}')
    echo "$dll: $(echo "$result" | jq -r '.repository_url // "N/A"')"
done
```

### Integration with Git
```bash
# Clone exact source version
clone_source() {
    local dll=$1
    local target_dir=$2
    
    if ! metadata=$(get-source-repository-url "$dll" 2>/dev/null); then
        echo "No repository metadata in $dll" >&2
        return 1
    fi
    
    local repo_url=$(echo "$metadata" | cut -d' ' -f1)
    local commit_sha=$(echo "$metadata" | cut -d' ' -f2)
    
    git clone "$repo_url" "$target_dir"
    cd "$target_dir" && git checkout "$commit_sha"
}

# Usage
clone_source MyApp.dll /tmp/myapp-source
```

## 🧪 Testing & Validation

### Known Working Assemblies
The tool has been tested with these assemblies from .NET installations:

- **Microsoft.AspNetCore.App.dll** - ASP.NET Core runtime
- **System.Text.Json.dll** - JSON serialization library  
- **Microsoft.Extensions.Hosting.dll** - Hosting abstractions

### Sample Expected Output
```bash
$ get-source-repository-url /usr/local/share/dotnet/shared/Microsoft.AspNetCore.App/9.0.6/Microsoft.AspNetCore.App.dll
https://github.com/dotnet/aspnetcore 5535e31a712343a63f5d7d796cd874e563e5ac14
```

### Running Tests
```bash
cd Tests/GetSourceRepositoryUrl.Tests
dotnet test
# Output: Passed! - Failed: 0, Passed: 10, Skipped: 0, Total: 10
```

## 🤖 For Coding Agents

This tool is specifically designed for AI coding agents and automated systems:

### Agent Integration Patterns

**Discovery**: Agents can use `--help` to understand the tool's capabilities
```bash
get-source-repository-url --help
# Detailed help explains metadata extraction and use cases
```

**Error Handling**: Exit codes enable proper error handling in scripts
```bash
if ! get-source-repository-url --quiet assembly.dll; then
    case $? in
        1) echo "Assembly file issue" ;;
        2) echo "No metadata available" ;;  
        3) echo "Invalid metadata format" ;;
    esac
fi
```

**Structured Output**: JSON mode provides parseable data
```bash
metadata=$(get-source-repository-url --json assembly.dll)
repo_url=$(echo "$metadata" | jq -r '.repository_url')
```

### Common Agent Workflows

1. **Source Code Analysis**
   ```bash
   # Agent discovers assembly, extracts source, analyzes code
   get-source-repository-url target.dll | while read repo commit; do
       git clone "$repo" /tmp/analysis
       cd /tmp/analysis && git checkout "$commit"
       # Perform analysis on exact source code
   done
   ```

2. **Dependency Tracking**
   ```bash
   # Map deployed assemblies to exact source versions
   find /app -name "*.dll" | while read dll; do
       if metadata=$(get-source-repository-url --json "$dll" 2>/dev/null); then
           echo "$(basename "$dll"): $metadata"
       fi
   done
   ```

3. **Security Auditing**
   ```bash
   # Verify assemblies match expected source versions
   expected_commit="abc123..."
   actual_commit=$(get-source-repository-url app.dll | cut -d' ' -f2)
   [ "$expected_commit" = "$actual_commit" ] || echo "Version mismatch!"
   ```

## 🔒 Security Considerations

- **Read-only**: Tool only reads assembly metadata, never executes code
- **No network access**: Works entirely offline with local files
- **PE validation**: Validates file format before processing
- **Input sanitization**: Handles malformed files gracefully

## 🛣️ Roadmap

- [ ] Support for NuGet packages (.nupkg files)
- [ ] Batch processing mode for multiple assemblies
- [ ] Integration with symbol servers
- [ ] Plugin system for custom metadata sources
- [ ] ARM64 and x64 native binaries

## 🤝 Contributing

Contributions welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

### Development Setup
```bash
git clone https://github.com/jflam/get-source-repository-url.git
cd get-source-repository-url
dotnet build
dotnet test Tests/GetSourceRepositoryUrl.Tests
```

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built with [System.Reflection.Metadata](https://www.nuget.org/packages/System.Reflection.Metadata) for PE parsing
- CLI powered by [System.CommandLine](https://www.nuget.org/packages/System.CommandLine)
- Inspired by the need to bridge compiled code and source repositories

---

**Perfect for coding agents seeking the source truth behind compiled assemblies!** 🔍✨