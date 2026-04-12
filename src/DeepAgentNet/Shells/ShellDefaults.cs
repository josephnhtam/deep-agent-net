namespace DeepAgentNet.Shells
{
    public static class ShellDefaults
    {
        public const string ToolName = "shell";

        public const string SystemPrompt = """
            ## `shell`

            You have access to a shell tool for executing commands in a terminal.

            IMPORTANT: The shell tool is for terminal operations like git, npm, docker, package managers, build tools, etc.
            DO NOT use it for file operations (reading, writing, editing, searching, finding files) — use the dedicated filesystem tools instead:
            - File search: Use glob (NOT find or ls via shell)
            - Content search: Use grep tool (NOT grep via shell)
            - Read files: Use read_file (NOT cat/head/tail via shell)
            - Edit files: Use edit_file (NOT sed/awk via shell)
            - Write files: Use write_file (NOT echo/printf redirection via shell)
            - List files: Use ls tool (NOT ls via shell)

            ## Security Rules

            - NEVER execute commands that access or transmit sensitive data (passwords, tokens, keys) unless the user explicitly instructs you to.
            - NEVER run destructive commands (rm -rf /, format, drop database, etc.) without explicit user confirmation.
            - NEVER pipe curl output directly into a shell (e.g. curl ... | sh).
            - NEVER modify system-level configuration files unless explicitly asked.

            ## Git Safety Protocol

            - NEVER update the git config.
            - NEVER run destructive or irreversible git commands (push --force, hard reset, etc.) unless the user explicitly requests them.
            - NEVER skip hooks (--no-verify, --no-gpg-sign, etc.) unless the user explicitly requests it.
            - NEVER force push to main/master. Warn the user if they request it.
            - NEVER commit changes unless the user explicitly asks you to.
            - Avoid git commit --amend. ONLY use --amend when ALL conditions are met:
              (1) The user explicitly requested amend, OR the commit succeeded but a pre-commit hook auto-modified files that need including.
              (2) The HEAD commit was created by you in this conversation.
              (3) The commit has NOT been pushed to the remote.
            - If a commit FAILED or was REJECTED by a hook, NEVER amend — fix the issue and create a NEW commit.
            - If you already pushed to the remote, NEVER amend unless the user explicitly requests it.
            - Never use git commands with the -i flag (like git rebase -i or git add -i) since they require interactive input which is not supported.

            ## Command Best Practices

            - Always quote file paths that contain spaces with double quotes.
            - Use the workingDirectory parameter to change directories instead of cd.
            - If commands are independent, make multiple parallel shell tool calls.
            - If commands depend on each other, chain them with && in a single call.
            - Use ; only when you need sequential execution but do not care if earlier commands fail.
            - DO NOT use newlines to separate commands (newlines are ok in quoted strings).
            """;

        public static string GetToolDescription(IReadOnlyList<string> shells) => $"""
            Executes a shell command with optional timeout, ensuring proper handling and security measures.

            Available shells: {string.Join(", ", shells)}.

            All commands run in the specified working directory. Use the workingDirectory parameter to set the directory — AVOID using `cd <directory> && <command>` patterns.

            Before executing a command:
            1. If the command will create new directories or files, first verify the parent directory exists using the ls tool.
            2. Always quote file paths that contain spaces with double quotes.

            Usage notes:
            - The command and workingDirectory arguments are required.
            - If timeout is not specified, the command will use the default timeout.
            - If the commands are independent, make multiple shell tool calls in parallel rather than chaining them.
            - If the commands depend on each other, chain them with && in a single call.
            - AVOID using `cd <directory> && <command>`. Use the workingDirectory parameter instead.
            - DO NOT use the shell tool for file operations — use the dedicated filesystem tools instead.
            """;
    }
}
