using System.Text.Json;
using System.Text.RegularExpressions;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Models.PromptRendering;

namespace PromptHub.Web.Services.PromptRendering;

public sealed partial class PromptTemplateRenderer
    : IPromptTemplateRenderer
{
    public PromptRenderResult Render(
        string templateText,
        IReadOnlyCollection<PromptVariable> variables,
        IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(variables);
        ArgumentNullException.ThrowIfNull(values);

        var errors = new List<PromptRenderError>();

        if (string.IsNullOrWhiteSpace(templateText))
        {
            errors.Add(new PromptRenderError(
                "template.empty",
                "Текст шаблона промпта не заполнен."));

            return PromptRenderResult.Failure(errors);
        }

        var variableMap = CreateVariableMap(
            variables,
            errors);

        if (errors.Count > 0)
        {
            return PromptRenderResult.Failure(errors);
        }

        var placeholderKeys = PlaceholderRegex()
        .Matches(templateText)
        .Cast<Match>()
        .Select(match => match.Groups["key"].Value)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var conditionalKeys = ConditionalBlockRegex()
            .Matches(templateText)
            .Cast<Match>()
            .Select(match => match.Groups["key"].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var referencedKeys = placeholderKeys
            .Concat(conditionalKeys)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        ValidateTemplateVariables(
            referencedKeys,
            variableMap,
            errors);

        if (errors.Count > 0)
        {
            return PromptRenderResult.Failure(errors);
        }

        var submittedValues =
            new Dictionary<string, string?>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var pair in values)
        {
            submittedValues[pair.Key] = pair.Value;
        }

        var normalizedValues =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var pair in variableMap)
        {
            var variable = pair.Value;

            submittedValues.TryGetValue(
                variable.Key,
                out var submittedValue);

            var normalizedValue =
                submittedValue?.Trim() ?? string.Empty;

            if (variable.IsRequired &&
                string.IsNullOrWhiteSpace(normalizedValue))
            {
                errors.Add(new PromptRenderError(
                    "value.required",
                    $"Поле «{variable.Label}» обязательно для заполнения.",
                    variable.Key));

                continue;
            }

            if (variable.VariableType ==
                    PromptVariableType.Select &&
                !string.IsNullOrWhiteSpace(normalizedValue))
            {
                ValidateSelectValue(
                    variable,
                    normalizedValue,
                    errors);
            }

            if (variable.VariableType ==
    PromptVariableType.Checkbox)
            {
                normalizedValue =
                    string.Equals(
                        normalizedValue,
                        "true",
                        StringComparison.OrdinalIgnoreCase)
                        ? "Да"
                        : string.Equals(
                            normalizedValue,
                            "false",
                            StringComparison.OrdinalIgnoreCase)
                            ? "Нет"
                            : normalizedValue;
            }

            normalizedValues[variable.Key] =
                normalizedValue;
        }

        if (errors.Count > 0)
        {
            return PromptRenderResult.Failure(errors);
        }

        var templateWithProcessedConditions =
            ConditionalBlockRegex().Replace(
                templateText,
                match =>
                {
                    var key = match.Groups["key"].Value;

                    if (!normalizedValues.TryGetValue(
                            key,
                            out var value) ||
                        string.IsNullOrWhiteSpace(value))
                    {
                        return string.Empty;
                    }

                    return match.Groups["content"].Value
                        .Trim('\r', '\n');
                });

        var renderedText = PlaceholderRegex().Replace(
            templateWithProcessedConditions,
            match =>
            {
                var key = match.Groups["key"].Value;

                return normalizedValues.TryGetValue(
                    key,
                    out var value)
                    ? value
                    : string.Empty;
            });

        renderedText = ExcessBlankLinesRegex().Replace(
            renderedText,
            Environment.NewLine + Environment.NewLine);

        return PromptRenderResult.Success(
            renderedText.Trim());
    }

    private static Dictionary<string, PromptVariable>
        CreateVariableMap(
            IEnumerable<PromptVariable> variables,
            ICollection<PromptRenderError> errors)
    {
        var result =
            new Dictionary<string, PromptVariable>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var variable in variables)
        {
            var key = variable.Key?.Trim()
                ?? string.Empty;

            if (!VariableKeyRegex().IsMatch(key))
            {
                errors.Add(new PromptRenderError(
                    "variable.invalid_key",
                    $"Недопустимое имя переменной: «{key}».",
                    key));

                continue;
            }

            if (!result.TryAdd(key, variable))
            {
                errors.Add(new PromptRenderError(
                    "variable.duplicate_key",
                    $"Переменная «{key}» описана несколько раз.",
                    key));
            }
        }

        return result;
    }

    private static void ValidateTemplateVariables(
        IReadOnlySet<string> placeholderKeys,
        IReadOnlyDictionary<string, PromptVariable> variableMap,
        ICollection<PromptRenderError> errors)
    {
        foreach (var placeholderKey in placeholderKeys)
        {
            if (!variableMap.ContainsKey(placeholderKey))
            {
                errors.Add(new PromptRenderError(
                    "template.undefined_variable",
                    $"Для переменной «{placeholderKey}» отсутствуют настройки.",
                    placeholderKey));
            }
        }

        foreach (var variableKey in variableMap.Keys)
        {
            if (!placeholderKeys.Contains(variableKey))
            {
                errors.Add(new PromptRenderError(
                    "variable.unused",
                    $"Переменная «{variableKey}» не используется в шаблоне.",
                    variableKey));
            }
        }
    }

    private static void ValidateSelectValue(
        PromptVariable variable,
        string submittedValue,
        ICollection<PromptRenderError> errors)
    {
        HashSet<string> allowedValues;

        try
        {
            allowedValues = ReadSelectOptions(
                variable.OptionsJson);
        }
        catch (JsonException)
        {
            errors.Add(new PromptRenderError(
                "variable.invalid_options",
                $"У поля «{variable.Label}» неправильно настроены варианты выбора.",
                variable.Key));

            return;
        }

        if (allowedValues.Count == 0)
        {
            errors.Add(new PromptRenderError(
                "variable.empty_options",
                $"У поля «{variable.Label}» не указаны варианты выбора.",
                variable.Key));

            return;
        }

        if (!allowedValues.Contains(submittedValue))
        {
            errors.Add(new PromptRenderError(
                "value.invalid_option",
                $"Выбрано недопустимое значение для поля «{variable.Label}».",
                variable.Key));
        }
    }

    private static HashSet<string> ReadSelectOptions(
        string optionsJson)
    {
        using var document =
            JsonDocument.Parse(optionsJson);

        if (document.RootElement.ValueKind !=
            JsonValueKind.Array)
        {
            throw new JsonException(
                "OptionsJson должен содержать массив.");
        }

        var values = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var option in
                 document.RootElement.EnumerateArray())
        {
            if (option.ValueKind !=
                JsonValueKind.Object)
            {
                continue;
            }

            if (!option.TryGetProperty(
                    "value",
                    out var valueProperty))
            {
                continue;
            }

            var value = valueProperty.GetString();

            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    [GeneratedRegex(
    @"\[\[if\s+(?<key>[A-Za-z][A-Za-z0-9_]*)\s*\]\](?<content>.*?)\[\[endif\]\]",
    RegexOptions.CultureInvariant |
    RegexOptions.IgnoreCase |
    RegexOptions.Singleline)]
    private static partial Regex ConditionalBlockRegex();

    [GeneratedRegex(
        @"(\r?\n){3,}",
        RegexOptions.CultureInvariant)]
    private static partial Regex ExcessBlankLinesRegex();

    [GeneratedRegex(
        @"\{\{\s*(?<key>[A-Za-z][A-Za-z0-9_]*)\s*\}\}",
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase)]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(
        @"^[A-Za-z][A-Za-z0-9_]*$",
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase)]
    private static partial Regex VariableKeyRegex();

}
