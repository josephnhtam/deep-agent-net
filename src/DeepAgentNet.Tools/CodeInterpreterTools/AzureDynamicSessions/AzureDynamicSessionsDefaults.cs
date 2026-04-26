namespace DeepAgentNet.Tools.CodeInterpreterTools.AzureDynamicSessions
{
    public static class AzureDynamicSessionsDefaults
    {
        public const string ExecuteCodeToolName = "execute_code";

        public const string DownloadFileToolName = "download_session_file";

        public const string ListFilesToolName = "list_session_files";

        public static string GetExecuteCodeToolDescription(string language) => $$"""
            Executes {{language}} code in a sandboxed code interpreter session.

            Usage notes:
            - Code runs in an isolated {{language}} environment.
            - You must read and write files at /mnt/data within the session (e.g. '/mnt/data/output.csv').
            - Use the 'files' parameter to upload local files into the session before execution. Each entry maps a local file path to a target file name; uploaded files are placed at /mnt/data/{fileName}.
            - Session state (variables, files, installed packages) persists across multiple calls within the same session.
            - Each execution has a maximum runtime of 220 seconds.
            - Always save output files to /mnt/data so they can be downloaded later.
            """;

        public const string DownloadFileToolDescription = """
            Downloads a file from the code interpreter session to the local filesystem.

            Usage notes:
            - Specify the file name (not a full path) to download.
            - The file is saved to the specified local file path.
            - If the file is not found, it was likely not saved to /mnt/data during code execution. Re-run your code and ensure output files are written to /mnt/data.
            """;

        public const string ListFilesToolDescription = """
            Lists all files in the code interpreter session.

            Use this tool to discover files created by code execution or previously uploaded to the session.
            Only files stored at /mnt/data are listed. If expected files are missing, ensure your code writes them to /mnt/data.
            """;

        public static string GetSystemPrompt(AzureDynamicSessionsInfo info)
        {
            string prompt = $$"""
                ## Code Interpreter (Azure Dynamic Sessions) Tools

                You have access to a sandboxed {{info.Language}} code interpreter session through the following tools:
                - `{{ExecuteCodeToolName}}`: Execute {{info.Language}} code. You can upload local files into the session using the 'files' parameter.
                - `{{DownloadFileToolName}}`: Download a file from the session to the local filesystem.
                - `{{ListFilesToolName}}`: List all files in the session.

                ### Sandboxed File System

                - Uploaded files are placed at /mnt/data/{fileName}.
                - Output files must be saved to /mnt/data in your code so they can be downloaded.
                - Use `{{ListFilesToolName}}` to discover available files.

                ### Workflow

                1. Use `{{ExecuteCodeToolName}}` to run code. Upload any needed local files using the 'files' parameter.
                2. When code produces output files (e.g. images, CSVs, reports), you MUST immediately download them to the local filesystem using `{{DownloadFileToolName}}`. Never just report file names without downloading.
                3. Use `{{ListFilesToolName}}` to discover files in the session if needed.

                ### Important

                - Always download generated files. Users cannot access the sandbox session directly. Writing files to the sandbox doesn't give anything to the users.
                - Never mention internal session details (e.g. paths, sandbox, /mnt/data) to the user. Refer to files by their local paths after downloading.
                - Session state (variables, files, installed packages) persists across calls.
                - Each execution has a maximum runtime of 220 seconds.
                """;

            if (info.PreinstalledPackages is { Count: > 0 })
            {
                prompt += $"""

                    ### Preinstalled Packages

                    The following packages are available in the session without installation:
                    {string.Join(", ", info.PreinstalledPackages)}
                    """;
            }

            if (!string.IsNullOrWhiteSpace(info.AdditionalInstructions))
            {
                prompt += $"""

                    ### Additional Instructions

                    {info.AdditionalInstructions}
                    """;
            }

            return prompt;
        }
    }
}
