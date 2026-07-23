namespace WindowsBlackHole.Core;

public sealed record DropValidationResult(
    bool IsValid,
    IReadOnlyList<string> Paths,
    string? Error);

public static class DropValidator
{
    public static DropValidationResult Validate(IEnumerable<string> candidates)
    {
        var accepted = new List<string>();

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(candidate);
            }
            catch (Exception exception) when (
                exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return Invalid($"路径无效：{candidate}");
            }

            var isFile = File.Exists(fullPath);
            var isDirectory = Directory.Exists(fullPath);
            if (!isFile && !isDirectory)
            {
                return Invalid($"文件或文件夹不存在：{fullPath}");
            }

            if (new Uri(fullPath).IsUnc)
            {
                return Invalid($"MVP 暂不处理网络路径：{fullPath}");
            }

            var root = Path.GetPathRoot(fullPath);
            if (root is not null &&
                string.Equals(
                    Path.TrimEndingDirectorySeparator(fullPath),
                    Path.TrimEndingDirectorySeparator(root),
                    StringComparison.OrdinalIgnoreCase))
            {
                return Invalid($"禁止删除磁盘根目录：{fullPath}");
            }

            if (isDirectory && IsProtectedTopLevelDirectory(fullPath))
            {
                return Invalid($"禁止删除系统或用户顶层目录：{fullPath}");
            }

            if (isDirectory &&
                (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
            {
                return Invalid($"MVP 暂不处理目录链接：{fullPath}");
            }

            accepted.Add(fullPath);
        }

        return accepted.Count == 0
            ? Invalid("没有可处理的本地文件或文件夹。")
            : new DropValidationResult(true, accepted, null);
    }

    private static bool IsProtectedTopLevelDirectory(string path)
    {
        var protectedPaths = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };

        var normalized = Path.TrimEndingDirectorySeparator(path);
        return protectedPaths
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(Path.GetFullPath)
            .Select(Path.TrimEndingDirectorySeparator)
            .Any(value => string.Equals(value, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static DropValidationResult Invalid(string error) =>
        new(false, Array.Empty<string>(), error);
}
