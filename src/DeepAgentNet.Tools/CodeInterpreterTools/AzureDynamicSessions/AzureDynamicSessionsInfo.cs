namespace DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions
{
    public record AzureDynamicSessionsInfo(
        string Language,
        IReadOnlyList<string>? PreinstalledPackages = null,
        string? AdditionalInstructions = null);
}
