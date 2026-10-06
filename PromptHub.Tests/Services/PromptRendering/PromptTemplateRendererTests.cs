using PromptHub.Web.Models.Entities;
using PromptHub.Web.Models.Enums;
using PromptHub.Web.Services.PromptRendering;

namespace PromptHub.Tests.Services.PromptRendering;

public sealed class PromptTemplateRendererTests
{
    private readonly PromptTemplateRenderer _renderer =
        new();

    [Fact]
    public void Render_ReplacesVariableInEveryOccurrence()
    {
        var variables = new[]
        {
            CreateVariable(
                "topic",
                "Тема работы")
        };

        var values =
            new Dictionary<string, string?>
            {
                ["topic"] =
                    "Разработка Telegram-бота"
            };

        var result = _renderer.Render(
            """
            Тема: {{topic}}.
            Повтор темы: {{ topic }}.
            """,
            variables,
            values);

        Assert.True(result.IsSuccess);

        Assert.Equal(
            """
            Тема: Разработка Telegram-бота.
            Повтор темы: Разработка Telegram-бота.
            """,
            result.RenderedText);
    }

    [Fact]
    public void Render_ReturnsError_WhenRequiredValueIsMissing()
    {
        var variables = new[]
        {
            CreateVariable(
                "topic",
                "Тема работы",
                isRequired: true)
        };

        var result = _renderer.Render(
            "Напиши текст на тему {{topic}}.",
            variables,
            new Dictionary<string, string?>());

        Assert.False(result.IsSuccess);

        Assert.Contains(
            result.Errors,
            error =>
                error.Code == "value.required" &&
                error.VariableKey == "topic");
    }

    [Fact]
    public void Render_ReturnsError_WhenTemplateVariableHasNoDefinition()
    {
        var result = _renderer.Render(
            "Тема: {{topic}}.",
            Array.Empty<PromptVariable>(),
            new Dictionary<string, string?>());

        Assert.False(result.IsSuccess);

        Assert.Contains(
            result.Errors,
            error =>
                error.Code ==
                    "template.undefined_variable" &&
                error.VariableKey == "topic");
    }

    [Fact]
    public void Render_ReplacesOptionalEmptyValueWithEmptyString()
    {
        var variables = new[]
        {
            CreateVariable(
                "requirements",
                "Дополнительные требования",
                isRequired: false)
        };

        var result = _renderer.Render(
            "Дополнительные требования: {{requirements}}",
            variables,
            new Dictionary<string, string?>());

        Assert.True(result.IsSuccess);

        Assert.Equal(
            "Дополнительные требования:",
            result.RenderedText);
    }

    [Fact]
    public void Render_ReturnsError_WhenSelectValueIsNotAllowed()
    {
        var variable = CreateVariable(
            "work_type",
            "Тип работы");

        variable.VariableType =
            PromptVariableType.Select;

        variable.OptionsJson =
            """
            [
              {
                "value": "coursework",
                "label": "Курсовая работа"
              },
              {
                "value": "thesis",
                "label": "ВКР"
              }
            ]
            """;

        var values =
            new Dictionary<string, string?>
            {
                ["work_type"] = "article"
            };

        var result = _renderer.Render(
            "Тип работы: {{work_type}}.",
            new[] { variable },
            values);

        Assert.False(result.IsSuccess);

        Assert.Contains(
            result.Errors,
            error =>
                error.Code ==
                    "value.invalid_option");
    }

    private static PromptVariable CreateVariable(
        string key,
        string label,
        bool isRequired = true)
    {
        return new PromptVariable
        {
            Key = key,
            Label = label,
            VariableType =
                PromptVariableType.Text,
            IsRequired = isRequired,
            OptionsJson = "[]"
        };
    }

    [Fact]
    public void Render_RemovesConditionalBlock_WhenValueIsEmpty()
    {
        var variables = new[]
        {
        CreateVariable(
            "requirements",
            "Дополнительные требования",
            isRequired: false)
    };

        var result = _renderer.Render(
            """
        Основная инструкция.

        [[if requirements]]
        Дополнительные требования:
        {{requirements}}
        [[endif]]

        Конец промпта.
        """,
            variables,
            new Dictionary<string, string?>());

        Assert.True(result.IsSuccess);

        Assert.DoesNotContain(
            "Дополнительные требования:",
            result.RenderedText);

        Assert.DoesNotContain(
            "[[if",
            result.RenderedText);

        Assert.Contains(
            "Конец промпта.",
            result.RenderedText);
    }

    [Fact]
    public void Render_KeepsConditionalBlock_WhenValueIsProvided()
    {
        var variables = new[]
        {
        CreateVariable(
            "requirements",
            "Дополнительные требования",
            isRequired: false)
    };

        var values = new Dictionary<string, string?>
        {
            ["requirements"] =
                "Добавить практическую главу."
        };

        var result = _renderer.Render(
            """
        Основная инструкция.

        [[if requirements]]
        Дополнительные требования:
        {{requirements}}
        [[endif]]

        Конец промпта.
        """,
            variables,
            values);

        Assert.True(result.IsSuccess);

        Assert.Contains(
            "Дополнительные требования:",
            result.RenderedText);

        Assert.Contains(
            "Добавить практическую главу.",
            result.RenderedText);

        Assert.DoesNotContain(
            "[[if",
            result.RenderedText);

        Assert.DoesNotContain(
            "[[endif]]",
            result.RenderedText);
    }
}