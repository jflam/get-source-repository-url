using System.CommandLine;
using GetSourceRepositoryUrl;

var assemblyPathArgument = new Argument<string>("assembly-path", "Path to the .NET assembly file");

var jsonOption = new Option<bool>("--json", "Output in JSON format instead of space-separated");
var verboseOption = new Option<bool>("--verbose", "Show diagnostic information to stderr");
var quietOption = new Option<bool>("--quiet", "Suppress all output except results and errors");

var rootCommand = new RootCommand("Extract repository URL and commit SHA from .NET assembly metadata")
{
    assemblyPathArgument,
    jsonOption,
    verboseOption,
    quietOption
};

rootCommand.SetHandler((string assemblyPath, bool json, bool verbose, bool quiet) =>
{
    try
    {
        if (verbose && !quiet)
        {
            OutputFormatter.WriteVerbose($"Reading assembly: {assemblyPath}");
        }

        var result = AssemblyMetadataReader.ExtractMetadata(assemblyPath);

        if (verbose && !quiet)
        {
            OutputFormatter.WriteVerbose($"Extraction result - Success: {result.Success}");
            if (result.RepositoryUrl != null)
                OutputFormatter.WriteVerbose($"Repository URL: {result.RepositoryUrl}");
            if (result.CommitSha != null)
                OutputFormatter.WriteVerbose($"Commit SHA: {result.CommitSha}");
            if (result.ErrorMessage != null)
                OutputFormatter.WriteVerbose($"Error: {result.ErrorMessage}");
        }

        if (!result.Success)
        {
            if (!quiet)
            {
                OutputFormatter.WriteError(result.ErrorMessage ?? "Unknown error");
            }

            // Determine appropriate exit code based on error type
            var exitCode = result.ErrorMessage switch
            {
                var msg when msg?.Contains("File not found") == true => 1,
                var msg when msg?.Contains("not a .NET assembly") == true => 1,
                var msg when msg?.Contains("Invalid PE file") == true => 1,
                var msg when msg?.Contains("No repository metadata") == true => 2,
                var msg when msg?.Contains("Incomplete repository metadata") == true => 3,
                _ => 1
            };

            Environment.Exit(exitCode);
            return;
        }

        // Both repository URL and commit SHA should be available if Success is true
        var output = json 
            ? OutputFormatter.FormatJson(result.RepositoryUrl!, result.CommitSha!)
            : OutputFormatter.FormatDefault(result.RepositoryUrl!, result.CommitSha!);

        Console.WriteLine(output);
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        if (!quiet)
        {
            OutputFormatter.WriteError($"Unexpected error: {ex.Message}");
        }
        Environment.Exit(1);
    }
}, assemblyPathArgument, jsonOption, verboseOption, quietOption);

// Add version information
rootCommand.Description = """
Extract repository URL and commit SHA from .NET assembly metadata.

Modern .NET assemblies (SDK 8+) embed build-time repository information that
can be used to locate the exact source code used to build the assembly.

This tool reads the following assembly attributes:
• AssemblyInformationalVersionAttribute - Contains commit SHA after '+'
• AssemblyMetadataAttribute("RepositoryUrl") - Contains repository URL

USAGE EXAMPLES:
  get-source-repository-url MyApp.dll
  → https://github.com/myorg/myapp abc123def456...

  # Use with git to clone and checkout exact source
  repo_url=$(get-source-repository-url MyApp.dll | cut -d' ' -f1)
  commit_sha=$(get-source-repository-url MyApp.dll | cut -d' ' -f2)
  git clone "$repo_url" /tmp/source
  cd /tmp/source && git checkout "$commit_sha"

  # JSON output for programmatic use
  get-source-repository-url --json MyApp.dll | jq -r '.repository_url'

EXIT CODES:
  0    Success - repository information extracted
  1    File not found or not a valid .NET assembly
  2    Assembly found but missing repository metadata
  3    Assembly contains malformed repository metadata
  4    Invalid command line arguments
""";

return rootCommand.Invoke(args);