using System.ComponentModel;

namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors
{
    [Description("The result of executing a SQL query")]
    public record SqlQueryResult(
        [Description("The column names in the result set")]
        IReadOnlyList<string> Columns,
        [Description("The rows of data returned by the query")]
        IReadOnlyList<IReadOnlyList<object?>> Rows,
        [Description("The number of rows returned")]
        int RowCount,
        [Description("The number of rows affected by the statement, or -1 for queries")]
        int AffectedRows
    );
}
