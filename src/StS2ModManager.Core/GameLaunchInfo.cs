namespace StS2ModManager.Core;

public sealed record GameLaunchInfo(
    string ExecutablePath,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory)
{
    public string ArgumentsText => string.Join(" ", Arguments.Select(QuoteIfNeeded));

    private static string QuoteIfNeeded(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "\"\"";
        }

        return value.Any(char.IsWhiteSpace) ? $"\"{value}\"" : value;
    }
}
