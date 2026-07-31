using System.Media;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using D2RBuffTracker.Models;
using D2RBuffTracker.Services;
using Wpf.Ui.Controls;

namespace D2RBuffTracker.Views;

public partial class BuffEditorWindow : FluentWindow
{
    private readonly TrackedBuff _buff;
    private readonly InputCaptureService _capture = new();

    private InputBinding? _selectKey;
    private InputBinding? _useKey;
    private string? _iconPath;
    private Action? _restoreActiveBind;
    private string _amberSoundName = "beep";
    private string _redSoundName = "warning";
    private string _expireSoundName = "expire";

    private BuffEditorWindow(TrackedBuff buff)
    {
        InitializeComponent();
        _buff = buff;

        NameBox.Text = buff.Name;
        DurationBox.Value = buff.Duration;
        EnabledSwitch.IsChecked = buff.IsEnabled;
        _selectKey = buff.SelectKey?.Clone();
        _useKey = buff.UseKey?.Clone();
        _iconPath = buff.IconPath;

        AmberSecondsBox.Value = buff.AmberWarningSeconds;
        RedSecondsBox.Value = buff.RedWarningSeconds;
        AmberSoundSwitch.IsChecked = buff.AmberSoundEnabled;
        RedSoundSwitch.IsChecked = buff.RedSoundEnabled;
        ExpireSoundSwitch.IsChecked = buff.ExpireSoundEnabled;
        _amberSoundName = buff.AmberSoundName;
        _redSoundName = buff.RedSoundName;
        _expireSoundName = buff.ExpireSoundName;

        UpdateIcon();
        UpdateKeyLabels();
        UpdateSoundLabels();

        Closed += (_, _) => _capture.Dispose();
    }

    public static bool Edit(Window? owner, TrackedBuff buff)
    {
        var dlg = new BuffEditorWindow(buff) { Owner = owner };
        return dlg.ShowDialog() == true;
    }

    private void UpdateIcon()
    {
        var resolved = AssetPaths.Resolve(_iconPath);
        if (resolved == null)
        {
            IconPreview.Source = null;
            return;
        }
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.UriSource = new Uri(resolved);
            img.EndInit();
            img.Freeze();
            IconPreview.Source = img;
        }
        catch
        {
            IconPreview.Source = null;
        }
    }

    private void UpdateKeyLabels()
    {
        SelectLabel.Text = _selectKey?.Display ?? "Not set";
        UseLabel.Text = _useKey?.Display ?? "Not set";
    }

    private void UpdateSoundLabels()
    {
        AmberSoundLabel.Text = AssetPaths.SoundDisplayName(_amberSoundName);
        RedSoundLabel.Text = AssetPaths.SoundDisplayName(_redSoundName);
        ExpireSoundLabel.Text = AssetPaths.SoundDisplayName(_expireSoundName);
    }

    private void PreviewAmber_OnClick(object sender, RoutedEventArgs e) => PlaySound(_amberSoundName);

    private void PreviewRed_OnClick(object sender, RoutedEventArgs e) => PlaySound(_redSoundName);

    private void PreviewExpire_OnClick(object sender, RoutedEventArgs e) => PlaySound(_expireSoundName);

    private void ChooseAmber_OnClick(object sender, RoutedEventArgs e)
    {
        var picked = SoundGalleryWindow.Pick(this, _amberSoundName);
        if (picked != null)
        {
            _amberSoundName = picked;
            UpdateSoundLabels();
        }
    }

    private void ChooseRed_OnClick(object sender, RoutedEventArgs e)
    {
        var picked = SoundGalleryWindow.Pick(this, _redSoundName);
        if (picked != null)
        {
            _redSoundName = picked;
            UpdateSoundLabels();
        }
    }

    private void ChooseExpire_OnClick(object sender, RoutedEventArgs e)
    {
        var picked = SoundGalleryWindow.Pick(this, _expireSoundName);
        if (picked != null)
        {
            _expireSoundName = picked;
            UpdateSoundLabels();
        }
    }

    private static void PlaySound(string? id)
    {
        try
        {
            var path = AssetPaths.Sound(id);
            if (System.IO.File.Exists(path))
            {
                using var player = new SoundPlayer(path);
                player.Play();
            }
        }
        catch (Exception ex)
        {
            Logger.Log(ex);
        }
    }

    private void BindSelect_OnClick(object sender, RoutedEventArgs e)
        => BeginCapture(SelectBindBtn, binding => { _selectKey = binding ?? _selectKey; UpdateKeyLabels(); });

    private void BindUse_OnClick(object sender, RoutedEventArgs e)
        => BeginCapture(UseBindBtn, binding => { _useKey = binding ?? _useKey; UpdateKeyLabels(); });

    private void BeginCapture(Button button, Action<InputBinding?> onResult)
    {
        // If another bind is already listening, restore its button first so it
        // isn't left stranded showing "Press a key..." after we start a new one.
        _restoreActiveBind?.Invoke();

        var originalContent = button.Content;
        button.Content = "Press a key... (Esc)";
        button.IsEnabled = false;

        void Restore()
        {
            button.Content = originalContent;
            button.IsEnabled = true;
        }

        _restoreActiveBind = Restore;

        _capture.Begin(Dispatcher, new WindowInteropHelper(this).Handle, binding =>
        {
            _restoreActiveBind = null;
            Restore();
            onResult(binding);
        });
    }

    private void ClearSelect_OnClick(object sender, RoutedEventArgs e)
    {
        _selectKey = null;
        UpdateKeyLabels();
    }

    private void ClearUse_OnClick(object sender, RoutedEventArgs e)
    {
        _useKey = null;
        UpdateKeyLabels();
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        _buff.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "Buff" : NameBox.Text.Trim();
        _buff.Duration = DurationBox.Value ?? _buff.Duration;
        _buff.IsEnabled = EnabledSwitch.IsChecked ?? true;
        _buff.SelectKey = _selectKey;
        _buff.UseKey = _useKey;
        _buff.IconPath = _iconPath;

        // Red first so the amber clamp (amber must stay above red) is consistent.
        _buff.RedWarningSeconds = (int)(RedSecondsBox.Value ?? _buff.RedWarningSeconds);
        _buff.AmberWarningSeconds = (int)(AmberSecondsBox.Value ?? _buff.AmberWarningSeconds);
        _buff.AmberSoundEnabled = AmberSoundSwitch.IsChecked ?? false;
        _buff.RedSoundEnabled = RedSoundSwitch.IsChecked ?? false;
        _buff.ExpireSoundEnabled = ExpireSoundSwitch.IsChecked ?? true;
        _buff.AmberSoundName = _amberSoundName;
        _buff.RedSoundName = _redSoundName;
        _buff.ExpireSoundName = _expireSoundName;

        DialogResult = true;
        Close();
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// Restore this buff's tunable settings (duration, warning thresholds and
    /// sounds) to their defaults. Name, icon and key bindings are left untouched.
    /// </summary>
    private void Reset_OnClick(object sender, RoutedEventArgs e)
    {
        DurationBox.Value = 30;
        EnabledSwitch.IsChecked = true;

        // Set red before amber so the "amber above red" clamp stays consistent.
        RedSecondsBox.Value = 5;
        AmberSecondsBox.Value = 10;
        AmberSoundSwitch.IsChecked = false;
        RedSoundSwitch.IsChecked = false;
        ExpireSoundSwitch.IsChecked = true;
        _amberSoundName = "beep";
        _redSoundName = "warning";
        _expireSoundName = "expire";
        UpdateSoundLabels();
    }
}
