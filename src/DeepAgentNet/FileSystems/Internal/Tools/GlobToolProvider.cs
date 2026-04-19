using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.Shared;
using DeepAgentNet.Shared.Internal;
using DeepAgentNet.Shared.Internal.Contracts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using FileSystemInfo = DeepAgentNet.FileSystems.Contracts.FileSystemInfo;

namespace DeepAgentNet.FileSystems.Internal.Tools
{
    internal class GlobToolProvider : IToolProvider
    {
        private readonly IFileSystemAccess _access;
        private readonly TokenLimitedToolOptions _options;
        private readonly ILogger<GlobToolProvider>? _logger;

        public AITool Tool { get; }

        public GlobToolProvider(IFileSystemAccess access, TokenLimitedToolOptions options, ILoggerFactory? loggerFactory = null)
        {
            _access = access;
            _options = options;
            _logger = loggerFactory?.CreateLogger<GlobToolProvider>();

            AIFunction function = AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
            {
                Name = FileSystemDefaults.GlobToolName,
                Description = options.Description ?? FileSystemDefaults.GlobToolDescription,
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
            [Description("Glob pattern (e.g., '*.py', '**/*.ts')")]
            string pattern,
            [Description("The path to the directory to search in")]
            string? path = null,
            string? cwdPath = null,
            CancellationToken cancellationToken = default)
        {
            path = await _access.ResolvePathAsync(path ?? ".", cwdPath, cancellationToken).ConfigureAwait(false);

            _logger?.Globbing(pattern, path);

            try
            {
                IStringBuilder sb = _options.ResultTokenLimit.HasValue ?
                    new TruncatingStringBuilder(
                        _options.ResultTokenLimit.Value * SharedConstants.ApproximateCharsPerToken,
                        SharedConstants.TruncationGuidance) :
                    new StandardStringBuilder();

                int matchCount = 0;

                await foreach (FileSystemInfo info in _access.GlobInfoAsync(pattern, path, cancellationToken).ConfigureAwait(false))
                {
                    matchCount++;

                    string line = $"{info.Path} ({info.Size} bytes)";

                    if (!sb.AppendLine(line))
                        break;
                }

                _logger?.GlobCompleted(pattern, matchCount);

                return matchCount > 0 ? sb.ToString() : $"No files found matching pattern '{pattern}'";
            }
            catch (Exception ex)
            {
                _logger?.GlobFailed(ex, pattern);

                return $"Error: {ex.Message}";
            }
        }
    }
}
