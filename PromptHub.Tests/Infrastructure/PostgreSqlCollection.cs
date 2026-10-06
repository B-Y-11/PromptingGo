namespace PromptHub.Tests.Infrastructure;

[CollectionDefinition(
    "PostgreSQL",
    DisableParallelization = true)]
public sealed class PostgreSqlCollection
    : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name =
        "PostgreSQL";
}