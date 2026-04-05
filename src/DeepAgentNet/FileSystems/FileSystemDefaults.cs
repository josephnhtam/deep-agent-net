namespace DeepAgentNet.FileSystems
{
    public static class FileSystemDefaults
    {
        public const string LsToolName = "ls";

        public const string ReadFileToolName = "read_file";

        public const string WriteFileToolName = "write_file";

        public const string EditFileToolName = "edit_file";

        public const string GlobToolName = "glob";

        public const string DeleteFileToolName = "delete_file";

        public const string GrepToolName = "grep";

        public const string SystemPrompt = $"""
            ## Filesystem Tools `{LsToolName}`, `{ReadFileToolName}`, `{WriteFileToolName}`, `{EditFileToolName}`, `{DeleteFileToolName}`, `{GlobToolName}`, `{GrepToolName}`

            You have access to a filesystem which you can interact with using these tools.
            All file paths must start with a /.

            - ls: list files and directories in a path (supports recursive listing)
            - read_file: read a file from the filesystem
            - write_file: write to a file in the filesystem
            - edit_file: edit a file in the filesystem
            - delete_file: delete a file from the filesystem
            - glob: find files matching a pattern (e.g., "**/*.py")
            - grep: search for text within files
            """;

        public const string LsToolDescription = """
            Lists files and directories in a given path. Directories are listed first, then files.
            Common directories like node_modules, .git, dist, build, etc. are automatically excluded.
            You can optionally provide an array of patterns to ignore with the ignore parameter.
            Set recursive=true to list all contents recursively.

            This is useful for exploring the filesystem and finding the right file to read or edit.
            You should almost ALWAYS use this tool before using the read_file or edit_file tools.
            """;

        public const string ReadFileToolDescription = """
            Reads a file from the filesystem.

            Assume this tool is able to read all files. If the User provides a path to a file assume that path is valid. It is okay to read a file that does not exist; an error will be returned.

            Usage:
            - By default, it reads up to 100 lines starting from the beginning of the file
            - **IMPORTANT for large files and codebase exploration**: Use pagination with offset and limit parameters to avoid context overflow
              - First scan: read_file(path, limit=100) to see file structure
              - Read more sections: read_file(path, offset=100, limit=200) for next 200 lines
              - Only omit limit (read full file) when necessary for editing
            - Specify offset and limit: read_file(path, offset=0, limit=100) reads first 100 lines
            - Results are returned using cat -n format, with line numbers starting at 1
            - Lines longer than 10,000 characters will be split into multiple lines with continuation markers (e.g., 5.1, 5.2, etc.). When you specify a limit, these continuation lines count towards the limit.
            - You have the capability to call multiple tools in a single response. It is always better to speculatively read multiple files as a batch that are potentially useful.
            - If you read a file that exists but has empty contents you will receive a system reminder warning in place of file contents.
            - You should ALWAYS make sure a file has been read before editing it.
            """;

        public const string WriteFileToolDescription = """
            Writes to a new file in the filesystem.

            Usage:
            - The write_file tool will create a new file.
            - Prefer to edit existing files (with the edit_file tool) over creating new ones when possible.
            """;

        public const string EditFileToolDescription = """
            Performs exact string replacements in files.

            Usage:
            - You must read the file before editing. This tool will error if you attempt an edit without reading the file first.
            - When editing, preserve the exact indentation (tabs/spaces) from the read output. Never include line number prefixes in old_string or new_string.
            - ALWAYS prefer editing existing files over creating new ones.
            - Only use emojis if the user explicitly requests it.
            """;

        public const string DeleteFileToolDescription = """
            Deletes a file from the filesystem.

            The file must exist. The operation will fail if the file does not exist or is a symlink.
            """;

        public const string GlobToolDescription = """
            Find files matching a glob pattern.

            Supports standard glob patterns: `*` (any characters), `**` (any directories), `?` (single character).
            Returns a list of absolute file paths that match the pattern.

            Examples:
            - `**/*.py` - Find all Python files
            - `*.txt` - Find all text files in root
            - `/subdir/**/*.md` - Find all markdown files under /subdir
            """;

        public const string GrepToolDescription = """
            Search for a pattern across files.

            By default, searches for literal text. Set isRegex=true to use regular expression matching.
            When using literal mode, special characters like parentheses, brackets, pipes, etc. are treated as literal characters.

            Examples:
            - Search all files: `grep(pattern="TODO")`
            - Search Python files only: `grep(pattern="import", glob="*.py")`
            - Regex search: `grep(pattern="function\\s+\\w+", isRegex=true)`
            - Search for code with special chars: `grep(pattern="def __init__(self):")`
            """;
    }
}
