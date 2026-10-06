using System.Text.Json;
using PromptHub.Web.Models.Enums;

namespace PromptHub.Web.Services.PromptTemplates;

public sealed class VisualPromptTemplateCompiler
    : IVisualPromptTemplateCompiler
{
    private const int MaximumFieldCount = 30;

    public VisualPromptTemplateCompileResult Compile(
        string displayTemplate,
        IReadOnlyList<VisualPromptFieldDefinition> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(displayTemplate))
        {
            errors.Add(
                "Добавьте текст настраиваемой версии.");

            return CreateFailure(errors);
        }

        if (fields.Count == 0)
        {
            errors.Add(
                "Добавьте хотя бы одно заполняемое поле.");

            return CreateFailure(errors);
        }

        if (fields.Count > MaximumFieldCount)
        {
            errors.Add(
                $"Можно добавить не более {MaximumFieldCount} полей.");

            return CreateFailure(errors);
        }

        var labels = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var preparedFields =
            new List<PreparedVisualField>();

        for (var index = 0;
             index < fields.Count;
             index++)
        {
            var field = fields[index];

            var label = field.Label?.Trim()
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(label))
            {
                errors.Add(
                    $"У поля №{index + 1} отсутствует название.");

                continue;
            }

            if (label.Length > 150)
            {
                errors.Add(
                    $"Название поля «{label}» слишком длинное.");

                continue;
            }

            if (label.Contains('[') ||
                label.Contains(']'))
            {
                errors.Add(
                    $"Название поля «{label}» не должно содержать квадратные скобки.");

                continue;
            }

            if (!labels.Add(label))
            {
                errors.Add(
                    $"Поле «{label}» добавлено несколько раз.");

                continue;
            }

            var placeholder =
                field.Placeholder?.Trim()
                ?? string.Empty;

            if (placeholder.Length > 300)
            {
                errors.Add(
                    $"Подсказка поля «{label}» не должна превышать 300 символов.");
            }

            var helpText =
                field.HelpText?.Trim()
                ?? string.Empty;

            if (helpText.Length > 500)
            {
                errors.Add(
                    $"Описание поля «{label}» не должно превышать 500 символов.");
            }

            var options = NormalizeOptions(
                field.Options);

            if (field.VariableType ==
                    PromptVariableType.Select &&
                options.Count < 2)
            {
                errors.Add(
                    $"Для списка «{label}» укажите минимум два варианта.");
            }

            var marker = $"[{label}]";

            if (!displayTemplate.Contains(
                    marker,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    $"Поле «{label}» не вставлено в текст промпта.");
            }

            preparedFields.Add(
                new PreparedVisualField(
                    Key: $"field_{index + 1}",
                    Marker: marker,
                    Label: label,
                    VariableType:
                        field.VariableType,
                    IsRequired:
                        field.IsRequired,
                    Placeholder:
                        placeholder,
                    HelpText:
                        helpText,
                    Options:
                        options,
                    SortOrder:
                        (index + 1) * 10));
        }

        if (errors.Count > 0)
        {
            return CreateFailure(errors);
        }

        var normalizedTemplate = displayTemplate
            .Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .Replace('\r', '\n');

        var sourceLines =
            normalizedTemplate.Split('\n');

        var compiledLines =
            new List<string>();

        foreach (var sourceLine in sourceLines)
        {
            var usedFields = preparedFields
                .Where(field =>
                    sourceLine.Contains(
                        field.Marker,
                        StringComparison.Ordinal))
                .ToArray();

            var optionalFields = usedFields
                .Where(field =>
                    !field.IsRequired)
                .ToArray();

            if (optionalFields.Length > 1)
            {
                errors.Add(
                    "На одной строке нельзя использовать несколько необязательных полей.");

                continue;
            }

            if (optionalFields.Length == 1 &&
                usedFields.Length > 1)
            {
                errors.Add(
                    $"Необязательное поле «{optionalFields[0].Label}» должно находиться на отдельной строке.");

                continue;
            }

            var compiledLine = sourceLine;

            foreach (var field in usedFields)
            {
                compiledLine = compiledLine.Replace(
                    field.Marker,
                    $"{{{{{field.Key}}}}}",
                    StringComparison.Ordinal);
            }

            if (optionalFields.Length == 1)
            {
                var optionalField =
                    optionalFields[0];

                compiledLines.Add(
                    $"[[if {optionalField.Key}]]");

                compiledLines.Add(
                    compiledLine);

                compiledLines.Add(
                    "[[endif]]");
            }
            else
            {
                compiledLines.Add(
                    compiledLine);
            }
        }

        if (errors.Count > 0)
        {
            return CreateFailure(errors);
        }

        var compiledVariables = preparedFields
            .Select(field =>
                new CompiledPromptVariable(
                    Key: field.Key,
                    Label: field.Label,
                    VariableType:
                        field.VariableType,
                    Placeholder:
                        field.Placeholder,
                    HelpText:
                        field.HelpText,
                    IsRequired:
                        field.IsRequired,
                    DefaultValue:
                        string.Empty,
                    OptionsJson:
                        CreateOptionsJson(
                            field.Options),
                    SortOrder:
                        field.SortOrder))
            .ToArray();

        return new VisualPromptTemplateCompileResult
        {
            TemplateText = string.Join(
                    Environment.NewLine,
                    compiledLines)
                .Trim(),

            Variables = compiledVariables
        };
    }

    private static IReadOnlyList<string>
        NormalizeOptions(
            IEnumerable<string>? sourceOptions)
    {
        if (sourceOptions is null)
        {
            return Array.Empty<string>();
        }

        return sourceOptions
            .Select(option =>
                option?.Trim()
                ?? string.Empty)
            .Where(option =>
                !string.IsNullOrWhiteSpace(option))
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToArray();
    }

    private static string CreateOptionsJson(
        IReadOnlyList<string> options)
    {
        if (options.Count == 0)
        {
            return "[]";
        }

        var jsonOptions = options
            .Select(option => new
            {
                value = option,
                label = option
            });

        return JsonSerializer.Serialize(
            jsonOptions);
    }

    private static VisualPromptTemplateCompileResult
        CreateFailure(
            IReadOnlyList<string> errors)
    {
        return new VisualPromptTemplateCompileResult
        {
            Errors = errors.ToArray()
        };
    }

    private sealed record PreparedVisualField(
        string Key,
        string Marker,
        string Label,
        PromptVariableType VariableType,
        bool IsRequired,
        string Placeholder,
        string HelpText,
        IReadOnlyList<string> Options,
        int SortOrder);
}
