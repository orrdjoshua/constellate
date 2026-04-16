using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Constellate.App;

/// <summary>
/// Partial definition of MainWindowViewModel containing shared helper/formatting
/// methods and the common OnPropertyChanged implementation. The remaining
/// responsibilities (engine sync, commands, layout, readouts, settings) stay in
/// the original partial declaration in MainWindow.axaml.cs for now and will be
/// moved here or into additional partials in subsequent passes.
/// </summary>
public sealed partial class MainWindowViewModel
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private static string FormatReady(bool ready) => ready ? "ready" : "blocked";

    private static string FormatVector3(Vector3 value) =>
        $"({value.X:0.##}, {value.Y:0.##}, {value.Z:0.##})";

    private static string FormatExpanded(bool expanded) => expanded ? "open" : "collapsed";

    private static string FormatFocusOrigin(string origin) =>
        string.IsNullOrWhiteSpace(origin)
            ? "unknown"
            : origin.Trim().ToLowerInvariant() switch
            {
                "mouse" => "mouse (viewport)",
                "keyboard" => "keyboard",
                "command" => "shell command",
                "programmatic" => "programmatic (engine/bookmark)",
                _ => origin.Trim()
            };

    /// <summary>
    /// Helper for expansion-state properties to update the backing field and raise OnPropertyChanged.
    /// </summary>
    private void SetExpansionState(ref bool backingField, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (backingField == value)
            return;

        backingField = value;
        OnPropertyChanged(propertyName);
    }

    private bool IsInteractionMode(string mode) =>
        string.Equals(
            _shellScene.GetInteractionMode(),
            mode,
            StringComparison.Ordinal);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName is not null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
