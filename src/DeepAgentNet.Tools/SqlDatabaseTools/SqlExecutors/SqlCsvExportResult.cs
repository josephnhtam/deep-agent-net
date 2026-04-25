using System.ComponentModel;

namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors
{
    [Description("The result of exporting SQL query results to a CSV file")]
    public record SqlCsvExportResult(
        [Description("The file path where the CSV was written")]
        string FilePath,
        [Description("The number of data rows written (excluding the header)")]
        int RowCount,
        [Description("The number of columns in the result set")]
        int ColumnCount
    );
}
