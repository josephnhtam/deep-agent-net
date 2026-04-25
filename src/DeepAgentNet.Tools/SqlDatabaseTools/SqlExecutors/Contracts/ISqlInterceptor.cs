namespace DeepAgentNet.Tools.SqlDatabaseTools.SqlExecutors.Contracts
{
    public interface ISqlInterceptor
    {
        string? Intercept(string sql, bool readOnly);
    }
}
