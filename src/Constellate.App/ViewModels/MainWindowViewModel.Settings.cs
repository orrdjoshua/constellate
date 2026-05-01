using System;
using Constellate.Core.Messaging;
using System.Linq;

namespace Constellate.App;

/// <summary>
/// Partial definition of MainWindowViewModel containing settings-related
/// properties and the EngineSettings bridge helpers. This logic previously
/// lived in MainWindow.axaml.cs and is now extracted into a dedicated
/// Settings partial as part of the MainWindow shell refactor.
/// </summary>
public sealed partial class MainWindowViewModel
{
    public bool MouseLeaveClearsFocus
    {
        get => _mouseLeaveClearsFocus;
        set
        {
            if (_mouseLeaveClearsFocus == value)
            {
                return;
            }

            _mouseLeaveClearsFocus = value;
            EngineServices.Settings.MouseLeaveClearsFocus = value;
            OnPropertyChanged();
            RaiseDerivedSettingsReadoutsChanged();
        }
    }

    public float GroupOverlayOpacity
    {
        get => _groupOverlayOpacity;
        set
        {
            var clamped = Math.Clamp(value, 0f, 1f);
            if (Math.Abs(_groupOverlayOpacity - clamped) < 0.0001f)
            {
                return;
            }

            _groupOverlayOpacity = clamped;
            EngineServices.Settings.GroupOverlayOpacity = clamped;
            OnPropertyChanged();
            RaiseDerivedSettingsReadoutsChanged();
        }
    }

    public float NodeHighlightOpacity
    {
        get => _nodeHighlightOpacity;
        set
        {
            var clamped = Math.Clamp(value, 0f, 1f);
            if (Math.Abs(_nodeHighlightOpacity - clamped) < 0.0001f)
            {
                return;
            }

            _nodeHighlightOpacity = clamped;
            EngineServices.Settings.NodeHighlightOpacity = clamped;
            OnPropertyChanged();
            RaiseDerivedSettingsReadoutsChanged();
        }
    }

    public float NodeFocusHaloRadiusMultiplier
    {
        get => _nodeFocusHaloRadiusMultiplier;
        set
        {
            var clamped = Math.Clamp(value, 0.5f, 3f);
            if (Math.Abs(_nodeFocusHaloRadiusMultiplier - clamped) < 0.0001f)
            {
                return;
            }

            _nodeFocusHaloRadiusMultiplier = clamped;
            EngineServices.Settings.NodeFocusHaloRadiusMultiplier = clamped;
            OnPropertyChanged();
            RaiseDerivedSettingsReadoutsChanged();
        }
    }

    public float NodeSelectionHaloRadiusMultiplier
    {
        get => _nodeSelectionHaloRadiusMultiplier;
        set
        {
            var clamped = Math.Clamp(value, 0.5f, 3f);
            if (Math.Abs(_nodeSelectionHaloRadiusMultiplier - clamped) < 0.0001f)
            {
                return;
            }

            _nodeSelectionHaloRadiusMultiplier = clamped;
            EngineServices.Settings.NodeSelectionHaloRadiusMultiplier = clamped;
            OnPropertyChanged();
            RaiseDerivedSettingsReadoutsChanged();
        }
    }

    public string NodeHaloMode
    {
        get => _nodeHaloMode;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? "2d"
                : value.Trim().ToLowerInvariant();

            if (!string.Equals(normalized, "2d", StringComparison.Ordinal) &&
                !string.Equals(normalized, "3d", StringComparison.Ordinal) &&
                !string.Equals(normalized, "both", StringComparison.Ordinal))
            {
                normalized = "2d";
            }

            if (string.Equals(_nodeHaloMode, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _nodeHaloMode = normalized;
            EngineServices.Settings.NodeHaloMode = normalized;
            OnPropertyChanged();
            RaiseDerivedSettingsReadoutsChanged();
        }
    }

    public string NodeHaloOcclusionMode
    {
        get => _nodeHaloOcclusionMode;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? "hollow"
                : value.Trim().ToLowerInvariant();

            if (!string.Equals(normalized, "hollow", StringComparison.Ordinal) &&
                !string.Equals(normalized, "occluding", StringComparison.Ordinal))
            {
                normalized = "hollow";
            }

            if (string.Equals(_nodeHaloOcclusionMode, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _nodeHaloOcclusionMode = normalized;
            EngineServices.Settings.NodeHaloOcclusionMode = normalized;
            OnPropertyChanged();
            RaiseDerivedSettingsReadoutsChanged();
        }
    }

    public float BackgroundAnimationSpeed
    {
        get => _backgroundAnimationSpeed;
        set
        {
            var clamped = Math.Clamp(value, 0f, 2f);
            if (Math.Abs(_backgroundAnimationSpeed - clamped) < 0.0001f)
            {
                return;
            }

            _backgroundAnimationSpeed = clamped;
            EngineServices.Settings.BackgroundAnimationSpeed = clamped;
            OnPropertyChanged();
            RaiseDerivedSettingsReadoutsChanged();
        }
    }

    public float LinkStrokeThickness
    {
        get => _linkStrokeThickness;
        set
        {
            var clamped = Math.Clamp(value, 0.5f, 4f);
            if (Math.Abs(_linkStrokeThickness - clamped) < 0.0001f)
            {
                return;
            }

            _linkStrokeThickness = clamped;
            EngineServices.Settings.LinkStrokeThickness = clamped;
            OnPropertyChanged();
            RaiseDerivedSettingsReadoutsChanged();
        }
    }

    public float LinkOpacity
    {
        get => _linkOpacity;
        set
        {
            var clamped = Math.Clamp(value, 0.1f, 1f);
            if (Math.Abs(_linkOpacity - clamped) < 0.0001f)
            {
                return;
            }

            _linkOpacity = clamped;
            EngineServices.Settings.LinkOpacity = clamped;
            OnPropertyChanged();
            RaiseDerivedSettingsReadoutsChanged();
        }
    }

    public float PaneletteBackgroundIntensity
    {
        get => _paneletteBackgroundIntensity;
        set
        {
            var clamped = Math.Clamp(value, 0.25f, 2f);
            if (Math.Abs(_paneletteBackgroundIntensity - clamped) < 0.0001f)
            {
                return;
            }

            _paneletteBackgroundIntensity = clamped;
            EngineServices.Settings.PaneletteBackgroundIntensity = clamped;
            OnPropertyChanged();
            RaiseDerivedSettingsReadoutsChanged();
        }
    }

    public float CommandSurfaceOverlayOpacity
    {
        get => _commandSurfaceOverlayOpacity;
        set
        {
            var clamped = Math.Clamp(value, 0.25f, 2f);
            if (Math.Abs(_commandSurfaceOverlayOpacity - clamped) < 0.0001f)
            {
                return;
            }

            _commandSurfaceOverlayOpacity = clamped;
            EngineServices.Settings.CommandSurfaceOverlayOpacity = clamped;
            OnPropertyChanged();
            RaiseDerivedSettingsReadoutsChanged();
        }
    }

    /// <summary>
    /// Refresh the ViewModel from EngineSettings and ShellScene state.
    /// This is called from the constructor and in response to settings
    /// or engine events; it was previously defined in MainWindow.axaml.cs.
    /// </summary>
    private void RefreshFromEngineState()
    {
        RefreshCapabilities();
        _mouseLeaveClearsFocus = EngineServices.Settings.MouseLeaveClearsFocus;
        OnPropertyChanged(nameof(MouseLeaveClearsFocus));
        _groupOverlayOpacity = EngineServices.Settings.GroupOverlayOpacity;
        OnPropertyChanged(nameof(GroupOverlayOpacity));
        _nodeHighlightOpacity = EngineServices.Settings.NodeHighlightOpacity;
        OnPropertyChanged(nameof(NodeHighlightOpacity));
        _nodeFocusHaloRadiusMultiplier = EngineServices.Settings.NodeFocusHaloRadiusMultiplier;
        OnPropertyChanged(nameof(NodeFocusHaloRadiusMultiplier));
        _nodeSelectionHaloRadiusMultiplier = EngineServices.Settings.NodeSelectionHaloRadiusMultiplier;
        OnPropertyChanged(nameof(NodeSelectionHaloRadiusMultiplier));
        _nodeHaloMode = EngineServices.Settings.NodeHaloMode;
        OnPropertyChanged(nameof(NodeHaloMode));
        _nodeHaloOcclusionMode = EngineServices.Settings.NodeHaloOcclusionMode;
        OnPropertyChanged(nameof(NodeHaloOcclusionMode));
        _backgroundAnimationSpeed = EngineServices.Settings.BackgroundAnimationSpeed;
        OnPropertyChanged(nameof(BackgroundAnimationSpeed));
        _linkStrokeThickness = EngineServices.Settings.LinkStrokeThickness;
        OnPropertyChanged(nameof(LinkStrokeThickness));
        _linkOpacity = EngineServices.Settings.LinkOpacity;
        OnPropertyChanged(nameof(LinkOpacity));
        _paneletteBackgroundIntensity = EngineServices.Settings.PaneletteBackgroundIntensity;
        OnPropertyChanged(nameof(PaneletteBackgroundIntensity));
        _commandSurfaceOverlayOpacity = EngineServices.Settings.CommandSurfaceOverlayOpacity;
        OnPropertyChanged(nameof(CommandSurfaceOverlayOpacity));
        RaiseDerivedSettingsReadoutsChanged();
        RaiseSceneStateChanged();
        RaiseCommandCanExecuteChanged();
    }

    private void RaiseDerivedSettingsReadoutsChanged()
    {
        OnPropertyChanged(nameof(VisualSemanticsSettingsSummary));
        OnPropertyChanged(nameof(RenderSurfaceSettingsSummary));
        OnPropertyChanged(nameof(SettingsSurfaceAuditSummary));
        OnPropertyChanged(nameof(ParentShellControlAuditSummary));
        OnPropertyChanged(nameof(MainWindowShellChromeAuditSummary));
        OnPropertyChanged(nameof(HardcodedSurfaceAuditNextTargetsSummary));
    }

    /// <summary>
    /// Refresh the Capabilities collection from EngineServices.
    /// </summary>
    private void RefreshCapabilities()
    {
        var latest = EngineServices.Capabilities.GetAll().ToArray();
        Capabilities.Clear();

        foreach (var capability in latest)
        {
            Capabilities.Add(capability);
        }
    }

    /// <summary>
    /// Apply a named background preset into EngineSettings, then refresh
    /// this ViewModel's settings from EngineSettings again.
    /// </summary>
    private void ApplyBackgroundPreset(string preset)
    {
        switch (preset)
        {
            case "DeepSpace":
                EngineServices.Settings.BackgroundMode = "gradient";
                EngineServices.Settings.BackgroundBaseColor = "#050911";
                EngineServices.Settings.BackgroundTopColor = "#0B1623";
                EngineServices.Settings.BackgroundBottomColor = "#050911";
                EngineServices.Settings.BackgroundAnimationMode = "slowlerp";
                EngineServices.Settings.BackgroundAnimationSpeed = 0.25f;
                break;
            case "Dusk":
                EngineServices.Settings.BackgroundMode = "gradient";
                EngineServices.Settings.BackgroundBaseColor = "#1A1024";
                EngineServices.Settings.BackgroundTopColor = "#302046";
                EngineServices.Settings.BackgroundBottomColor = "#080611";
                EngineServices.Settings.BackgroundAnimationMode = "slowlerp";
                EngineServices.Settings.BackgroundAnimationSpeed = 0.35f;
                break;
            case "Paper":
                EngineServices.Settings.BackgroundMode = "solid";
                EngineServices.Settings.BackgroundBaseColor = "#F5F5F2";
                EngineServices.Settings.BackgroundTopColor = "#F5F5F2";
                EngineServices.Settings.BackgroundBottomColor = "#F5F5F2";
                EngineServices.Settings.BackgroundAnimationMode = "off";
                EngineServices.Settings.BackgroundAnimationSpeed = 0.0f;
                break;
            default:
                return;
        }

        RefreshFromEngineState();
    }
}
