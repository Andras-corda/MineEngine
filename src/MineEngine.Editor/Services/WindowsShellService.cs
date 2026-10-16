using System.Diagnostics;

namespace MineEngine.Editor.Services;

public sealed class WindowsShellService : IShellService
{
    public void OpenFolder(string directory)
    {
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
    }

    public void OpenWith(string program, string file)
    {
        if (!File.Exists(program))
        {
            throw new FileNotFoundException(Tr.F("Programme introuvable : {0}", program), program);
        }

        var start = new ProcessStartInfo(program) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(file) ?? string.Empty };
        start.ArgumentList.Add(file);
        Process.Start(start);
    }

    public void ShowOpenWithDialog(string file)
    {
        if (File.Exists(file))
        {
            // OpenAs_RunDLL lit le reste de la ligne comme chemin : il ne faut pas de guillemets.
            Process.Start(new ProcessStartInfo("rundll32.exe", "shell32.dll,OpenAs_RunDLL " + file) { UseShellExecute = false });
        }
    }

    public void OpenUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
        {
            Process.Start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true });
        }
    }

    public void OpenFile(string file)
    {
        if (File.Exists(file))
        {
            Process.Start(new ProcessStartInfo { FileName = file, UseShellExecute = true });
        }
    }
}
