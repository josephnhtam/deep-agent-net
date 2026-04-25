using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors.Internal
{
    internal static class SqlCsvWriter
    {
        public static async Task<(int RowCount, int ColumnCount)> WriteAsync(
            Stream stream,
            IAsyncEnumerable<SqlRow> rows,
            CancellationToken cancellationToken = default)
        {
            StreamWriter writer = new(stream, leaveOpen: true);
            await using var _ = writer.ConfigureAwait(false);

            CsvConfiguration config = new(CultureInfo.InvariantCulture);
            using var csv = new CsvWriter(writer, config);

            bool headerWritten = false;
            int rowCount = 0;
            int columnCount = 0;

            await foreach (var row in rows.WithCancellation(cancellationToken))
            {
                if (!headerWritten)
                {
                    columnCount = row.Columns.Count;

                    foreach (var column in row.Columns)
                        csv.WriteField(column);

                    await csv.NextRecordAsync();
                    headerWritten = true;
                }

                foreach (var value in row.Values)
                    csv.WriteField(value);

                await csv.NextRecordAsync();
                rowCount++;
            }

            return (rowCount, columnCount);
        }
    }
}
