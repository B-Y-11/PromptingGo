using PromptHub.Web.Services.PromptTemplates;

namespace PromptHub.Tests.Services.PromptTemplates;

public sealed class PromptTemplateVariableDetectorTests
{
    private readonly PromptTemplateVariableDetector
        _detector = new();

    [Fact]
    public void Detect_FindsRequiredVariables()
    {
        var result = _detector.Detect(
            "Тема: {{topic}}. Стиль: {{style}}.");

        Assert.Collection(
            result,
            topic =>
            {
                Assert.Equal("topic", topic.Key);
                Assert.True(topic.IsRequired);
            },
            style =>
            {
                Assert.Equal("style", style.Key);
                Assert.True(style.IsRequired);
            });
    }

    [Fact]
    public void Detect_MarksConditionalVariableAsOptional()
    {
        var result = _detector.Detect(
            """
            Тема: {{topic}}.

            [[if requirements]]
            Требования:
            {{requirements}}
            [[endif]]
            """);

        var requirements = Assert.Single(
            result,
            variable =>
                variable.Key == "requirements");

        Assert.False(requirements.IsRequired);
    }

    [Fact]
    public void Detect_ReturnsVariableOnlyOnce()
    {
        var result = _detector.Detect(
            "{{topic}} — повтор: {{ topic }}");

        var variable = Assert.Single(result);

        Assert.Equal("topic", variable.Key);
    }
}