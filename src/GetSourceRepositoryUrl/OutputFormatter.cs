using System.Text.Json;

namespace GetSourceRepositoryUrl;

public static class OutputFormatter
{
    public static string FormatDefault(string repositoryUrl, string commitSha)
    {
        return $"{repositoryUrl} {commitSha}";
    }

    public static string FormatJson(string repositoryUrl, string commitSha)
    {
        var data = new
        {
            repository_url = repositoryUrl,
            commit_sha = commitSha
        };

        return JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    public static void WriteError(string message, TextWriter? errorWriter = null)
    {
        var writer = errorWriter ?? Console.Error;
        writer.WriteLine($"get-source-repository-url: {message}");
    }

    public static void WriteVerbose(string message, TextWriter? errorWriter = null)
    {
        var writer = errorWriter ?? Console.Error;
        writer.WriteLine($"[VERBOSE] {message}");
    }
}