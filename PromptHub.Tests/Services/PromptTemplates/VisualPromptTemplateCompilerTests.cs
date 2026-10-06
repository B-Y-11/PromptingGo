using PromptHub.Web.Models.Enums;
using PromptHub.Web.Services.PromptTemplates;
using System.Text.Json;

namespace PromptHub.Tests.Services.PromptTemplates;

public sealed class VisualPromptTemplateCompilerTests
{
    private readonly VisualPromptTemplateCompiler
        _compiler = new();

    [Fact]
    public void Compile_ConvertsReadableMarker()
    {
        var fields =
            new List<VisualPromptFieldDefinition>
            {
                new()
                {
                    Label = "Тема статьи",
                    VariableType =
                        PromptVariableType.Text,
                    IsRequired = true
                }
            };

        var result = _compiler.Compile(
            "Напиши статью на тему [Тема статьи].",
            fields);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);

        Assert.Equal(
            "Напиши статью на тему {{field_1}}.",
            result.TemplateText);

        var variable =
            Assert.Single(result.Variables);

        Assert.Equal(
            "field_1",
            variable.Key);

        Assert.Equal(
            "Тема статьи",
            variable.Label);
    }

    [Fact]
    public void Compile_WrapsOptionalFieldLine()
    {
        var fields =
            new List<VisualPromptFieldDefinition>
            {
                new()
                {
                    Label =
                        "Дополнительные требования",
                    VariableType =
                        PromptVariableType.TextArea,
                    IsRequired = false
                }
            };

        var result = _compiler.Compile(
            """
            Основной текст.

            Дополнительные требования: [Дополнительные требования]
            """,
            fields);

        Assert.True(result.IsSuccess);

        Assert.Contains(
            "[[if field_1]]",
            result.TemplateText);

        Assert.Contains(
            "Дополнительные требования: {{field_1}}",
            result.TemplateText);

        Assert.Contains(
            "[[endif]]",
            result.TemplateText);
    }

    [Fact]
    public void Compile_CreatesSelectOptionsJson()
    {
        var fields =
            new List<VisualPromptFieldDefinition>
            {
            new()
            {
                Label = "Стиль",
                VariableType =
                    PromptVariableType.Select,
                IsRequired = true,

                Options = new List<string>
                {
                    "Деловой",
                    "Дружелюбный"
                }
            }
            };

        var result = _compiler.Compile(
            "Стиль текста: [Стиль]",
            fields);

        Assert.True(result.IsSuccess);

        var variable =
            Assert.Single(result.Variables);

        using var document =
            JsonDocument.Parse(variable.OptionsJson);

        var options = document.RootElement
            .EnumerateArray()
            .ToArray();

        Assert.Equal(2, options.Length);

        Assert.Equal(
            "Деловой",
            options[0]
                .GetProperty("value")
                .GetString());

        Assert.Equal(
            "Деловой",
            options[0]
                .GetProperty("label")
                .GetString());

        Assert.Equal(
            "Дружелюбный",
            options[1]
                .GetProperty("value")
                .GetString());

        Assert.Equal(
            "Дружелюбный",
            options[1]
                .GetProperty("label")
                .GetString());
    }

    [Fact]
    public void Compile_RejectsUnusedField()
    {
        var fields =
            new List<VisualPromptFieldDefinition>
            {
                new()
                {
                    Label = "Аудитория",
                    VariableType =
                        PromptVariableType.Text,
                    IsRequired = true
                }
            };

        var result = _compiler.Compile(
            "Напиши рекламный текст.",
            fields);

        Assert.False(result.IsSuccess);

        Assert.Contains(
            result.Errors,
            error => error.Contains(
                "не вставлено",
                StringComparison.OrdinalIgnoreCase));
    }
}