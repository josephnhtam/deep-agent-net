using DeepAgentNet.FileSystems.Contracts;
using DeepAgentNet.Shared;
using DeepAgentNet.Shared.Internal;
using DeepAgentNet.Shared.Internal.Contracts;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using FileSystemInfo = DeepAgentNet.FileSystems.Contracts.FileSystemInfo;

namespace DeepAgentNet.FileSystems.Internal.Tools
{
    internal class ListInfoToolProvider : IToolProvider
    {
        private readonly IFileSystemAccess _access;
        private readonly TokenLimitedToolOptions _options;
        private readonly ILogger<ListInfoToolProvider>? _logger;

        public AITool Tool { get; }

        public ListInfoToolProvider(IFileSystemAccess access, TokenLimitedToolOptions options, ILoggerFactory? loggerFactory = null)
        {
            _access = access;
            _options = options;
            _logger = loggerFactory?.CreateLogger<ListInfoToolProvider>();

            AIFunction function = AIFunctionFactory.Create(ExecuteAsync, new AIFunctionFactoryOptions
            {
                Name = FileSystemDefaults.LsToolName,
                Description = options.Description ?? FileSystemDefaults.LsToolDescription,
                JsonSchemaCreateOptions = CreateJsonSchemaOptions()
            });

            Tool = options.ApprovalPolicy == ToolApprovalPolicy.Required ?
                new ApprovalRequiredAIFunction(function) : function;
        }

        private AIJsonSchemaCreateOptions CreateJsonSchemaOptions() => new()
        {
            ParameterDescriptionProvider = property => property.Name switch
            {
                "cwdPath" => $"The working directory for resolving relative paths. Defaults to '{_access.RootWorkingDirectory}'.",
                _ => null
            }
        };

        private async ValueTask<string> ExecuteAsync(
            [Description("The path to the directory to list")]
            string path,
            [Description("Whether to list recursively (default: false)")]
            bool recursive = false,
            string? cwdPath = null,
            CancellationToken cancellationToken = default)
        {
            path = await _access.ResolvePathAsync(path, cwdPath, cancellationToken).ConfigureAwait(false);

            _logger?.ListingDirectory(path, recursive);

            try
            {
                IStringBuilder sb = _options.ResultTokenLimit.HasValue ?
                    new TruncatingStringBuilder(
                        _options.ResultTokenLimit.Value * SharedConstants.ApproximateCharsPerToken,
                        SharedConstants.TruncationGuidance) :
                    new StandardStringBuilder();

                int entryCount = 0;
                string basePath = path.TrimEnd('/');
                LsState? lsState = GetOrCreateLsState();

                lsState?.Record(basePath);

                await foreach (FileSystemInfo info in
                    _access.ListInfoAsync(path, recursive, cancellationToken: cancellationToken).ConfigureAwait(false))
                {
                    entryCount++;

                    if (info.IsDirectory)
                        lsState?.Record(Path.Combine(basePath, info.Path));

                    string line = info switch
                    {
                        { IsDirectory: true } => $"{info.Path} (directory)",
                        { IsDirectory: false, Size: > 0 } => $"{info.Path} ({info.Size} bytes)",
                        _ => info.Path
                    };

                    if (!sb.AppendLine(line))
                        break;
                }

                _logger?.ListDirectoryCompleted(path, entryCount);

                return entryCount > 0 ? sb.ToString() : $"No files found in {path}";
            }
            catch (Exception ex)
            {
                _logger?.ListDirectoryFailed(ex, path);

                return $"Error: {ex.Message}";
            }
        }

        private static LsState? GetOrCreateLsState()
        {
            var session = AIAgent.CurrentRunContext?.Session;
            if (session is null)
                return null;

            var state = session.StateBag.GetValue<LsState>(LsState.StateBagKey);
            if (state is null)
            {
                state = new LsState();
                session.StateBag.SetValue(LsState.StateBagKey, state);
            }

            return state;
        }
    }
}
