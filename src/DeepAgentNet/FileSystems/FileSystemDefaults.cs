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

        public const string OverwriteFileToolName = "overwrite_file";

        public const string SystemPrompt = """
            ## Filesystem Tools `ls`, `read_file`, `write_file`, `edit_file`, `glob`, `delete_file`, `grep`, `overwrite_file`

            You have access to a filesystem which you can interact with using these tools.
            All file paths must start with a /.

            When reading files, each line in the output is prefixed in the format `#<line_number>:<content>`. Treat the `#<line_number>:` prefix as metadata -- it is not part of the actual file content. Do not include line number prefixes when providing text for edits.

            - ls: list files and directories in a path (supports recursive listing)
            - read_file: read a file from the filesystem
            - write_file: write a new file to the filesystem
            - overwrite_file: replace the entire content of an existing file
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
            - By default, it reads up to 500 lines starting from the beginning of the file.
            - The offset parameter is the line number to start from (0-indexed).
            - To read later sections, call this tool again with a larger offset.
            - Use the grep tool to find specific content in large files.
            - Contents are returned with each line prefixed by its 1-based line number in the format `#<line_number>:<content>`. For example, if a file contains "  hello\n  world", you will receive:
              #1:  hello
              #2:  world
              The `#<line_number>:` prefix is metadata added by this tool and is NOT part of the actual file content.
            - Lines longer than 10,000 characters will be split into multiple lines with continuation markers (e.g., #5.1:, #5.2:, etc.). When you specify a limit, these continuation lines count towards the limit.
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

        public const string OverwriteFileToolDescription = """
            Replaces the entire content of an existing file.

            Usage:
            - The file must already exist. Use write_file to create new files.
            - This completely replaces all file content with the provided content.
            - Prefer edit_file for targeted changes to specific parts of a file.
            - Use this tool when you need to rewrite the entire file.
            """;

        public const string EditFileToolDescription = """
            Performs exact string replacements in files.

            Usage:
            - You must read the file before editing. This tool will error if you attempt an edit without reading the file first.
            - CRITICAL: The read_file output prefixes every line in the format `#<line_number>:<content>`. These prefixes are metadata, NOT part of the file. You must NEVER include line number prefixes in old_string or new_string. Only use the text that appears AFTER the `#<line_number>:` prefix.
            - Preserve the exact indentation (tabs/spaces) as it appears in the actual file content (after the line number prefix).
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
