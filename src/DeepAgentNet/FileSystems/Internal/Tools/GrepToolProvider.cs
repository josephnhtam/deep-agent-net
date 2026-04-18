using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.Shared;
using DeepAgentNet.Shared.Internal;
using DeepAgentNet.Shared.Internal.Contracts;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace DeepAgentNet.FileSystems.Internal.Tools
{
    internal class GrepToolProvider : IToolProvider
    {
        private readonly IFileSystemAccess _access;
        private readonly TokenLimitedToolOptions _options;

        public AITool Tool { get; }

        public GrepToolProvider(IFileSystemAccess access, TokenLimitedToolOptions options)
        {
            _access = access;
            _options = options;

            AIFunction function = AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
            {
                Name = FileSystemDefaults.GrepToolName,
                Description = options.Description ?? FileSystemDefaults.GrepToolDescription,
                JsonSchemaCreateOptions = CreateJsonSchemaOptions()
            });

            Tool = options.ApprovalPolicy == ToolApprovalPolicy.Required ?
                new ApprovalRequiredAIFunction(function) : function;
        }

        private AIJsonSchemaCreateOptions CreateJsonSchemaOptions() => new()
        {
            ParameterDescriptionProvider = property => property.Name switch
            {
                "cwdPath" => $"The base working directory for resolving relative paths (including path). Defaults to '{_access.RootWorkingDirectory}'.",
                _ => null
            }
        };

        private async ValueTask<string> ExecuteAsync(
            [Description("The search pattern. Literal text by default, or a regular expression if isRegex is true.")]
            string pattern,
            [Description("The path to the directory to search in")]
            string? path = null,
            [Description("Optional glob pattern to filter files (e.g., '*.py')")]
            string? glob = null,
            [Description("If true, treat pattern as a regular expression. Defaults to false (literal text search).")]
            bool isRegex = false,
            string? cwdPath = null,
            CancellationToken cancellationToken = default)
        {
            if (path is null || !Path.IsPathFullyQualified(path))
                path = Path.Combine(cwdPath ?? _access.RootWorkingDirectory, path ?? ".");

            try
            {
                List<GrepMatch> matches = await _access.GrepAsync(pattern, path, glob, isRegex, cancellationToken).ConfigureAwait(false);

                if (!matches.Any())
                    return $"No matches found for pattern '{pattern}'";

                IStringBuilder sb = _options.ResultTokenLimit.HasValue ?
                    new TruncatingStringBuilder(
                        _options.ResultTokenLimit.Value * SharedConstants.ApproximateCharsPerToken,
                        SharedConstants.TruncationGuidance) :
                    new StandardStringBuilder();

                string? currentPath = null;
                foreach (GrepMatch match in matches)
                {
                    if (currentPath != match.Path)
                    {
                        currentPath = match.Path;

                        if (!sb.AppendLine($"\nFile: {currentPath}"))
                            break;
                    }

                    if (!sb.AppendLine($"  {match.Line}: {match.Text}"))
                        break;
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}
