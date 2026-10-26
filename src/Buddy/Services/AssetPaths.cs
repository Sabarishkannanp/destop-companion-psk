using System.IO;
namespace Buddy.Services;
public static class AssetPaths
{
    public static string Root { get; } = Extract();
    private static string Extract()
    {
        var assembly = typeof(AssetPaths).Assembly;
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Buddy", "Assets");
        var prefix = assembly.GetName().Name + ".Assets.";
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix,StringComparison.Ordinal)))
        {
            var relative = name[prefix.Length..];
            if (relative.StartsWith("Avatar.",StringComparison.Ordinal)) relative = Path.Combine("Avatar",relative[7..]);
            var destination = Path.Combine(root,relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            if (File.Exists(destination)) continue;
            using var source = assembly.GetManifestResourceStream(name)!;
            using var target = File.Create(destination); source.CopyTo(target);
        }
        return root;
    }
}
