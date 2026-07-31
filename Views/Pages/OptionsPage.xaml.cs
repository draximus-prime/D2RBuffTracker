using System.Windows;
using System.Windows.Controls;
using D2RBuffTracker.Models;
using D2RBuffTracker.ViewModels;

namespace D2RBuffTracker.Views;

public partial class OptionsPage : UserControl
{
    public OptionsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Vm == null)
            return;
        ScaleSlider.Value = Vm.Settings.OverlayScale;
        UpdateScaleLabel(Vm.Settings.OverlayScale);
        DigitSizeSlider.Value = Vm.Settings.DigitFontSize;
        UpdateDigitSizeLabel(Vm.Settings.DigitFontSize);

        ThemeCombo.SelectedIndex = Vm.Settings.Theme switch
        {
            AppTheme.Light => 1,
            AppTheme.System => 2,
            _ => 0
        };
    }

    private void UpdateScaleLabel(double scale) => ScaleValue.Text = $"{scale:0.0}x";

    private void UpdateDigitSizeLabel(double size) => DigitSizeValue.Text = $"{size:0}";

    private void DigitSizeSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateDigitSizeLabel(e.NewValue);
        if (!IsLoaded || Vm == null)
            return;
        Vm.Settings.DigitFontSize = e.NewValue;
        Vm.RefreshOverlayDigits();
        Vm.Settings.Save();
    }

    private void ScaleSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateScaleLabel(e.NewValue);
        if (IsLoaded)
            Vm?.UpdateOverlayScale(e.NewValue);
    }

    private void Save_OnChanged(object sender, RoutedEventArgs e) => Vm?.Settings.Save();

    private void Digits_OnChanged(object sender, RoutedEventArgs e)
    {
        Vm?.RefreshOverlayDigits();
        Vm?.Settings.Save();
    }

    private void Swipe_OnChanged(object sender, RoutedEventArgs e)
    {
        Vm?.RefreshOverlaySwipe();
        Vm?.Settings.Save();
    }

    private void Layout_OnChanged(object sender, RoutedEventArgs e)
    {
        Vm?.RefreshOverlayLayout();
        Vm?.Settings.Save();
    }

    private void Theme_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Vm == null || !IsLoaded)
            return;
        var theme = ThemeCombo.SelectedIndex switch
        {
            1 => AppTheme.Light,
            2 => AppTheme.System,
            _ => AppTheme.Dark
        };
        Vm.Settings.Theme = theme;
        App.ApplyTheme(theme);
        Vm.Settings.Save();
    }

    private void Reset_OnClick(object sender, RoutedEventArgs e)
    {
        if (Vm == null)
            return;

        var confirm = MessageBox.Show(
            Window.GetWindow(this),
            "Restore all overlay and appearance options to their default values?",
            "Reset options",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        Vm.Settings.ResetDisplayDefaults();

        // Unbound controls: assigning these fires their ValueChanged/SelectionChanged
        // handlers, which apply scale/digit-size/theme and save.
        ScaleSlider.Value = Vm.Settings.OverlayScale;
        DigitSizeSlider.Value = Vm.Settings.DigitFontSize;
        ThemeCombo.SelectedIndex = 0;

        // The bound toggles update automatically, but their Click handlers don't
        // fire on a programmatic change, so push the overlay state through here.
        Vm.RefreshOverlayLayout();
        Vm.RefreshOverlayDigits();
        Vm.RefreshOverlaySwipe();
        Vm.Settings.Save();
    }
}
