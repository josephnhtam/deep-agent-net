using System.ComponentModel;

namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlInspectors
{
    [Description("Information about a database schema")]
    public sealed record SqlSchemaInfo(
        [Description("The name of the schema")]
        string Name,
        [Description("The owner of the schema")]
        string Owner
    );

    [Description("Information about a database table")]
    public sealed record SqlTableInfo(
        [Description("The schema containing the table")]
        string Schema,
        [Description("The name of the table")]
        string Name
    );

    [Description("Information about a column in a database table")]
    public sealed record SqlColumnInfo(
        [Description("The name of the column")]
        string Name,
        [Description("The data type of the column")]
        string DataType,
        [Description("Whether the column allows null values")]
        bool IsNullable,
        [Description("Whether the column is part of the primary key")]
        bool IsPrimaryKey,
        [Description("The ordinal position of the column in the table")]
        int OrdinalPosition,
        [Description("The maximum character length of the column, if applicable")]
        int? MaxLength = null,
        [Description("The default value expression for the column, if any")]
        string? DefaultValue = null
    );

    [Description("Information about a constraint on a database table")]
    public sealed record SqlConstraintInfo(
        [Description("The name of the constraint")]
        string Name,
        [Description("The type of the constraint")]
        SqlConstraintType Type,
        [Description("The columns involved in the constraint")]
        IReadOnlyList<string> Columns,
        [Description("The referenced schema for foreign key constraints")]
        string? RefSchema = null,
        [Description("The referenced table for foreign key constraints")]
        string? RefTable = null,
        [Description("The referenced columns for foreign key constraints")]
        IReadOnlyList<string>? RefColumns = null
    );

    [Description("Information about an index on a database table")]
    public sealed record SqlIndexInfo(
        [Description("The name of the index")]
        string Name,
        [Description("The index method (e.g. btree, hash, gin)")]
        string Method,
        [Description("Whether the index enforces uniqueness")]
        bool IsUnique,
        [Description("Whether this is the primary key index")]
        bool IsPrimary,
        [Description("The columns included in the index")]
        IReadOnlyList<string> Columns
    );

    [Description("Detailed schema information for a database table including columns, constraints, and indexes")]
    public sealed record SqlTableSchemaInfo(
        [Description("The schema containing the table")]
        string Schema,
        [Description("The name of the table")]
        string Name,
        [Description("The columns in the table")]
        IReadOnlyList<SqlColumnInfo> Columns,
        [Description("The constraints defined on the table")]
        IReadOnlyList<SqlConstraintInfo> Constraints,
        [Description("The indexes defined on the table")]
        IReadOnlyList<SqlIndexInfo> Indexes
    );

    [Description("Size and row count statistics for a database table")]
    public sealed record SqlTableStats(
        [Description("The schema containing the table")]
        string Schema,
        [Description("The name of the table")]
        string Name,
        [Description("The estimated number of rows in the table")]
        long EstimatedRowCount,
        [Description("The total size of the table in bytes")]
        long TotalBytes,
        [Description("The total size of the table in a human-readable format")]
        string TotalSizePretty
    );

    [Description("The type of SQL constraint")]
    public enum SqlConstraintType
    {
        PrimaryKey,
        ForeignKey,
        Unique,
        Check,
        Exclude
    }
}
