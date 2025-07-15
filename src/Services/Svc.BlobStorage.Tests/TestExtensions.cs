namespace Svc.BlobStorage.Tests;

public static class TestExtensions
{
    public static void DeleteAll(this string path)
    {
        var di = new DirectoryInfo(path);
        if (!di.Exists) return;

        foreach (var file in di.GetFiles())
            file.Delete();

        foreach (var dir in di.GetDirectories())
            dir.Delete(true);
    }
}