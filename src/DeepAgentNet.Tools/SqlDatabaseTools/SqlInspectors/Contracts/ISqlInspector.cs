namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlInspectors.Contracts
{
    public interface ISqlInspector
    {
        string Dialect { get; }

        ValueTask<IReadOnlyList<SqlSchemaInfo>> ListSchemasAsync(
            CancellationToken cancellationToken = default);

        ValueTask<IReadOnlyList<SqlTableInfo>> ListTablesAsync(
            string? schema = null,
            CancellationToken cancellationToken = default);

        ValueTask<SqlTableSchemaInfo> GetTableSchemaAsync(
            string table,
            string? schema = null,
            CancellationToken cancellationToken = default);

        ValueTask<SqlTableSchemaInfo> GetTableSchemaAsync(
            SqlTableInfo table,
            CancellationToken cancellationToken = default);

        ValueTask<SqlTableStats> GetTableStatsAsync(
            string table,
            string? schema = null,
            CancellationToken cancellationToken = default);
    }
}
