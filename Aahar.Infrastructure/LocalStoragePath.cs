namespace Aahar.Infrastructure;

internal static class LocalStoragePath
{
    public static string ResolveDirectory(string path)
    {
        var full = Path.GetFullPath(path);
        var parent = Path.GetDirectoryName(full);
        if (parent is null) return full;
        var candidate = Path.Combine(ResolveDirectory(parent), Path.GetFileName(full));
        try { return new DirectoryInfo(candidate).ResolveLinkTarget(true)?.FullName ?? candidate; }
        catch (DirectoryNotFoundException) { return candidate; }
    }

    public static bool IsInside(string path, string parent)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(parent), path);
        return relative == "." || (relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar) && !Path.IsPathRooted(relative));
    }
}
