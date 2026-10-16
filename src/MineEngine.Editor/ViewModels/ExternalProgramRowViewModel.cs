using MineEngine.Editor.Mvvm;
using MineEngine.Editor.Settings;

namespace MineEngine.Editor.ViewModels;

/// <summary>Un programme de « Ouvrir avec » dans l'onglet Programmes des paramètres : nom et extensions modifiables.</summary>
public sealed class ExternalProgramRowViewModel : ObservableObject
{
    private readonly Action _changed;
    private string _name;
    private string _extensions;

    public ExternalProgramRowViewModel(ExternalProgram program, Action changed)
    {
        ArgumentNullException.ThrowIfNull(program);
        _changed = changed ?? throw new ArgumentNullException(nameof(changed));
        _name = program.Name;
        _extensions = program.Extensions;
        Path = program.Path;
    }

    public string Path { get; }

    public bool Exists => File.Exists(Path);

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value ?? string.Empty))
            {
                _changed();
            }
        }
    }

    /// <summary>Extensions séparées par des points-virgules ("png;jpg"), ou "*" pour tous les fichiers.</summary>
    public string Extensions
    {
        get => _extensions;
        set
        {
            if (SetProperty(ref _extensions, value ?? string.Empty))
            {
                _changed();
            }
        }
    }

    public ExternalProgram ToModel() =>
        new(_name.Trim().Length > 0 ? _name.Trim() : System.IO.Path.GetFileNameWithoutExtension(Path), Path, _extensions.Trim());
}
