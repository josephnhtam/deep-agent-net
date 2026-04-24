namespace DeepAgentNet.Tools.SqlDatabaseTools
{
    public static class SqlDatabaseDefaults
    {
        public const string QuerySqlToolName = "query_sql";

        public const string ExecuteSqlToolName = "execute_sql";

        public const string ListSchemasToolName = "list_schemas";

        public const string ListTablesToolName = "list_tables";

        public const string GetTableSchemaToolName = "get_table_schema";

        public const string GetTableStatsToolName = "get_table_stats";

        public const string QuerySqlToolDescription = """
            Executes a read-only SQL query against the connected database and returns the result set.

            Usage notes:
            - Use this tool for SELECT queries to retrieve data.
            - Prefer explicit column lists over SELECT *.
            - Always include a LIMIT clause or use the maxRows parameter to avoid returning excessively large result sets.
            - This tool runs in read-only mode. Use the execute_sql tool for write operations.
            """;

        public const string ExecuteSqlToolDescription = """
            Executes a SQL write statement (INSERT, UPDATE, DELETE) against the connected database.

            Usage notes:
            - Use this tool only for DML statements that modify data.
            - NEVER run DELETE or UPDATE without a WHERE clause unless the user explicitly confirms.
            - NEVER execute DDL statements (CREATE, ALTER, DROP) or administrative commands unless the user explicitly requests it.
            - For read-only SELECT queries, use the query_sql tool instead.
            """;

        public const string ListSchemasToolDescription = """
            Lists all user-defined schemas in the connected database.

            Use this tool to discover available schemas before exploring tables.
            """;

        public const string ListTablesToolDescription = """
            Lists all tables in the connected database, optionally filtered by schema.

            Use this tool to discover available tables before querying or inspecting their structure.
            """;

        public const string GetTableSchemaToolDescription = """
            Returns detailed schema information for a specific table, including columns, constraints, and indexes.

            Use this tool to understand a table's structure before writing queries against it.
            """;

        public const string GetTableStatsToolDescription = """
            Returns row count and size statistics for a specific table.

            Use this tool to understand data volumes before writing queries that may return large result sets.
            """;

        public static string GetSystemPrompt(string dialect) => $"""
            ## SQL Database Tools

            You have access to a {dialect} database through the following tools:
            - `{QuerySqlToolName}`: Execute read-only SQL queries.
            - `{ExecuteSqlToolName}`: Execute SQL write statements (INSERT, UPDATE, DELETE). This tool may not be available in read-only mode.
            - `{ListSchemasToolName}`: List database schemas.
            - `{ListTablesToolName}`: List tables in the database.
            - `{GetTableSchemaToolName}`: Get detailed column, constraint, and index information for a table.
            - `{GetTableStatsToolName}`: Get row count and size statistics for a table.

            ## Workflow

            1. Use `{ListSchemasToolName}` and `{ListTablesToolName}` to discover the database structure.
            2. Use `{GetTableSchemaToolName}` to understand table columns, constraints, and indexes before writing queries.
            3. Use `{QuerySqlToolName}` to run read-only queries and retrieve data.
            4. Use `{ExecuteSqlToolName}` only when the user explicitly asks to modify data.

            ## Safety Rules

            - Default to read-only queries using `{QuerySqlToolName}`.
            - Only use `{ExecuteSqlToolName}` for write operations when explicitly instructed by the user.
            - NEVER execute DDL statements (CREATE, ALTER, DROP) or administrative commands unless the user explicitly requests it.
            - NEVER run DELETE or UPDATE without a WHERE clause unless the user explicitly confirms.
            - Always use LIMIT or the maxRows parameter to prevent returning excessively large result sets.

            ## Best Practices

            - For complex data analysis, plan your approach first: identify the relevant tables, understand their relationships, and break the analysis into smaller steps before writing queries.
            - Inspect the schema first to understand table structures before writing queries.
            - Use explicit column lists instead of SELECT *.
            - Use table aliases for readability in multi-table queries.
            - Use appropriate JOIN types instead of subqueries when possible.
            - For multi-step analysis, start with exploratory queries to validate assumptions before building complex aggregations.
            - Format SQL for readability with proper indentation.
            """;
    }
}
