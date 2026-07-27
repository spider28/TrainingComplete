using System.Text.RegularExpressions;

namespace TrainingCompletion.Infrastructure;

internal static partial class SafeError
{
    public static string From(Exception exception)
    {
        var text = $"{exception.GetType().Name}: {exception.Message}";
        text = SecretPattern().Replace(text, "$1=***");
        return text.Length <= 500 ? text : text[..500];
    }

    [GeneratedRegex(
        @"(?i)\b(password|pwd|sharedaccesskey|accountkey|clientsecret)=([^;,\s]+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex SecretPattern();
}
