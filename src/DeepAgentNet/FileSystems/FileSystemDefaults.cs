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

            When reading files, results are returned with each line prefixed by a padded line number followed by an arrow (→), starting at 1. The line number prefix format is: spaces + line number + arrow (→). Treat the line number prefix as metadata -- it is not part of the actual file content. Do not include line number prefixes when providing text for edits.

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
            - Results are returned with each line prefixed by its line number and an arrow (→). The line number prefix is metadata, not file content.
            - You have the capability to call multiple tools in a single response. It is always better to speculatively read multiple files as a batch that are potentially useful.
            - You should ALWAYS make sure a file has been read before editing it.
            """;

        public const string WriteFileToolDescription = """
            Writes to a new file in the filesystem.

            Usage:
            - The write_file tool will create a new file.
            - You must list the parent directory (or an ancestor directory recursively) with ls before writing a new file. This tool will error if the parent directory has not been explored first.
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
            Performs exact string replacements in files. oldString is matched verbatim in the file and replaced with newString. Only the matched portion is affected.

            Rules:
            - You must read the file with read_file before editing. Edits will be rejected if the file has not been read first.
            - The read_file output prefixes each line with a line number and arrow (→). These prefixes are NOT part of the file content. Never include them in oldString or newString. Preserve the exact indentation (tabs/spaces) as it appears after the prefix.
            - oldString must match the file content exactly and must be unique in the file. If it is not unique, provide more surrounding lines as context to make it unique, or set replaceAll=true.
            - oldString should be 2-5 adjacent lines — enough to uniquely identify the location. Avoid overly large context (10+ lines) when fewer lines suffice.
            - To delete code, oldString must contain the ENTIRE block being removed (not just the first line), and newString should be empty.
            - Use replaceAll=true when renaming a variable or replacing a short string across the entire file.
            - Prefer editing existing files over creating new ones.

            Examples:
            - Replace a function's body (oldString includes all lines being changed):
                oldString: "function greet() {\n    return 'hello';\n}"
                newString: "function greet() {\n    return 'hi';\n}"
            - Delete an entire function (oldString must cover the full function, not just its first line):
                oldString: "function unused() {\n    doStuff();\n    return;\n}"
                newString: ""
            - Rename a variable everywhere in the file:
                oldString: "oldVar"
                newString: "newVar"
                replaceAll: true
            """;

        public const string DeleteFileToolDescription = """
            Deletes a file from the filesystem.

            The file must exist. The operation will fail if the file does not exist or is a symlink.

            Rules:
            - You must read the file with read_file before deleting. Deletions will be rejected if the file has not been read first.
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
