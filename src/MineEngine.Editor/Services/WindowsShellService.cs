using System.Diagnostics;

namespace MineEngine.Editor.Services;

public sealed class WindowsShellService : IShellService
{
    public void OpenFolder(string directory)
    {
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
    }

    public void OpenFile(string file)
    {
        if (File.Exists(file))
        {
            Process.Start(new ProcessStartInfo { FileName = file, UseShellExecute = true });
        }
    }
}
