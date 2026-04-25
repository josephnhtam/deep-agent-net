namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors
{
    public record SqlRow(
        IReadOnlyList<string> Columns,
        IReadOnlyList<object?> Values
    );
}
