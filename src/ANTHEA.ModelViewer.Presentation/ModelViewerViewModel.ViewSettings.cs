using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;

namespace ANTHEA.ModelViewer.Presentation;

public sealed partial class ModelViewerViewModel
{
    public event EventHandler? ViewSettingsChanged;
    public event EventHandler<string>? CameraPresetRequested;
    public IReadOnlyList<string> RotationModes { get; } = ["Verticale Z stabile", "Rotazione libera"];
    public IReadOnlyList<string> AntialiasingModes { get; } = ["Disattivato", "FXAA · veloce", "MSAA 4×", "MSAA 8×"];
    public IReadOnlyList<string> LightingModes { get; } = ["Piatto", "Shaded", "Render"];
    public IReadOnlyList<string> ContourPalettes { get; } = ["Spettro", "Blu / rosso", "Viridis"];
    [ObservableProperty] private bool showViewSettings;
    [ObservableProperty] private string sceneBackground = "#131D2B";
    [ObservableProperty] private string backgroundInput = "#131D2B";
    [ObservableProperty] private string viewSettingsMessage = "";
    [ObservableProperty] private double rotationSensitivity = .5;
    [ObservableProperty] private double panSensitivity = .75;
    [ObservableProperty] private double zoomSensitivity = .7;
    [ObservableProperty] private int selectedRotationMode;
    [ObservableProperty] private bool cameraInertia;
    [ObservableProperty] private bool rotateAroundPointer;
    [ObservableProperty] private bool isOrthographic;
    [ObservableProperty] private int selectedAntialiasing = 2;
    [ObservableProperty] private int selectedLighting = 1;
    [ObservableProperty] private int selectedContourPalette;
    [ObservableProperty] private int isolineCount = 8;
    [ObservableProperty] private double isolineWidth = .5;
    public string SceneInk
    {
        get
        {
            int rgb = int.Parse(SceneBackground[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return ((rgb >> 16) * .2126 + ((rgb >> 8) & 255) * .7152 + (rgb & 255) * .0722) > 150 ? "#24354A" : "#D0DDED";
        }
    }
    void ApplyViewSettings() { if (!disposed) ViewSettingsChanged?.Invoke(this, EventArgs.Empty); }
    partial void OnSceneBackgroundChanged(string value) { OnPropertyChanged(nameof(SceneInk)); ApplyViewSettings(); }
    partial void OnRotationSensitivityChanged(double value) => ApplyViewSettings();
    partial void OnPanSensitivityChanged(double value) => ApplyViewSettings();
    partial void OnZoomSensitivityChanged(double value) => ApplyViewSettings();
    partial void OnSelectedRotationModeChanged(int value) => ApplyViewSettings();
    partial void OnCameraInertiaChanged(bool value) => ApplyViewSettings();
    partial void OnRotateAroundPointerChanged(bool value) => ApplyViewSettings();
    partial void OnIsOrthographicChanged(bool value) => ApplyViewSettings();
    partial void OnSelectedAntialiasingChanged(int value) => ApplyViewSettings();
    partial void OnSelectedLightingChanged(int value) { ApplyViewSettings(); RefreshScene(); }
    partial void OnSelectedContourPaletteChanged(int value) => RefreshScene();
    partial void OnIsolineCountChanged(int value) => RefreshScene();
    partial void OnIsolineWidthChanged(double value) => RefreshScene();
    [RelayCommand] void ToggleViewSettings() { OpenNavigationCommand.Execute("2"); ShowViewSettings = false; }
    partial void OnShowViewSettingsChanged(bool value) { if(value) { OpenNavigationCommand.Execute("2"); ShowViewSettings=false; } }
    [RelayCommand] void ApplyBackground(string? preset)
    {
        string text = (preset ?? BackgroundInput).Trim();
        if (text.Length != 7 || text[0] != '#' || !int.TryParse(text[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        { ViewSettingsMessage = "Usa un colore nel formato #RRGGBB."; return; }
        SceneBackground = BackgroundInput = text.ToUpperInvariant(); ViewSettingsMessage = "";
    }
    [RelayCommand] void CameraPreset(string? preset)
    {
        if (preset is "front" or "back" or "top" or "bottom" or "left" or "right" or "iso-ne" or "iso-nw" or "iso-se" or "iso-sw")
            CameraPresetRequested?.Invoke(this, preset);
    }
    [RelayCommand] void ResetViewSettings()
    {
        SceneBackground = BackgroundInput = "#131D2B"; RotationSensitivity = .5; PanSensitivity = .75; ZoomSensitivity = .7;
        SelectedRotationMode = 0; CameraInertia = RotateAroundPointer = IsOrthographic = false; SelectedAntialiasing = 2;
        SelectedLighting = 1;
        ViewSettingsMessage = "";
    }
}
