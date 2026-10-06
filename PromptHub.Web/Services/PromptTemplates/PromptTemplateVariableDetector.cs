using System.Text.RegularExpressions;

namespace PromptHub.Web.Services.PromptTemplates;

public sealed partial class PromptTemplateVariableDetector
    : IPromptTemplateVariableDetector
{
    public IReadOnlyList<DetectedPromptVariable> Detect(
        string templateText)
    {
        if (string.IsNullOrWhiteSpace(templateText))
        {
            return Array.Empty<DetectedPromptVariable>();
        }

        var conditionalKeys = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (Match conditionalMatch in
                 ConditionalBlockRegex().Matches(templateText))
        {
            conditionalKeys.Add(
                conditionalMatch.Groups["key"].Value);

            var content =
                conditionalMatch.Groups["content"].Value;

            foreach (Match placeholderMatch in
                     PlaceholderRegex().Matches(content))
            {
                conditionalKeys.Add(
                    placeholderMatch.Groups["key"].Value);
            }
        }

        var templateWithoutConditions =
            ConditionalBlockRegex().Replace(
                templateText,
                string.Empty);

        var requiredKeys = PlaceholderRegex()
            .Matches(templateWithoutConditions)
            .Cast<Match>()
            .Select(match =>
                match.Groups["key"].Value)
            .ToHashSet(
                StringComparer.OrdinalIgnoreCase);

        var occurrences =
            new List<(string Key, int Index)>();

        foreach (Match match in
                 PlaceholderRegex().Matches(templateText))
        {
            occurrences.Add((
                match.Groups["key"].Value,
                match.Index));
        }

        foreach (Match match in
                 ConditionalBlockRegex().Matches(templateText))
        {
            occurrences.Add((
                match.Groups["key"].Value,
                match.Index));
        }

        var orderedKeys = occurrences
            .OrderBy(item => item.Index)
            .Select(item => item.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return orderedKeys
            .Select((key, index) =>
                new DetectedPromptVariable(
                    Key: key,
                    IsRequired:
                        requiredKeys.Contains(key),
                    SortOrder: (index + 1) * 10))
            .ToArray();
    }

    [GeneratedRegex(
        @"\{\{\s*(?<key>[A-Za-z][A-Za-z0-9_]*)\s*\}\}",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(
        @"\[\[if\s+(?<key>[A-Za-z][A-Za-z0-9_]*)\s*\]\](?<content>.*?)\[\[endif\]\]",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant |
        RegexOptions.Singleline)]
    private static partial Regex ConditionalBlockRegex();
}
