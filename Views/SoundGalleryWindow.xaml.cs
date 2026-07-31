using System.Media;
using System.Windows;
using System.Windows.Controls;
using D2RBuffTracker.Services;
using Wpf.Ui.Controls;

namespace D2RBuffTracker.Views;

public partial class SoundGalleryWindow : FluentWindow
{
    private readonly List<AssetPaths.SoundEntry> _all;
    private string? _selectedId;

    private SoundGalleryWindow(string? current)
    {
        InitializeComponent();
        _all = AssetPaths.AvailableSounds().ToList();
        _selectedId = current;

        BuildCategoryList();
        Loaded += (_, _) =>
        {
            CategoryList.SelectedIndex = 0;
            if (!string.IsNullOrWhiteSpace(_selectedId))
            {
                SelectedLabel.Text = AssetPaths.SoundDisplayName(_selectedId);
                UseButton.IsEnabled = true;
                PreviewButton.IsEnabled = true;
            }
            SearchBox.Focus();
        };
    }

    /// <summary>Show the gallery and return the chosen sound id, or null if cancelled.</summary>
    public static string? Pick(Window? owner, string? current)
    {
        var dlg = new SoundGalleryWindow(current) { Owner = owner };
        return dlg.ShowDialog() == true ? dlg._selectedId : null;
    }

    private void BuildCategoryList()
    {
        var categories = new List<string> { "All sounds" };
        categories.AddRange(_all.Select(s => s.Category).Distinct().OrderBy(c => c));
        CategoryList.ItemsSource = categories;
    }

    private void CategoryList_OnSelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshItems();

    private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e) => RefreshItems();

    private void RefreshItems()
    {
        var category = CategoryList.SelectedItem as string;
        var search = SearchBox.Text?.Trim() ?? string.Empty;

        IEnumerable<AssetPaths.SoundEntry> source = _all;
        if (!string.IsNullOrEmpty(category) && category != "All sounds")
            source = source.Where(s => s.Category == category);
        if (!string.IsNullOrEmpty(search))
            source = source.Where(s => s.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase));

        SoundItems.ItemsSource = source.ToList();
    }

    private void SoundTile_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string id)
            return;
        _selectedId = id;
        SelectedLabel.Text = AssetPaths.SoundDisplayName(id);
        UseButton.IsEnabled = true;
        PreviewButton.IsEnabled = true;
        Play(id);
    }

    private void Preview_OnClick(object sender, RoutedEventArgs e) => Play(_selectedId);

    private void Browse_OnClick(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Pick a sound file",
            Filter = "Wave audio (*.wav)|*.wav",
            CheckFileExists = true
        };
        if (dlg.ShowDialog(this) == true)
        {
            _selectedId = dlg.FileName;
            SelectedLabel.Text = AssetPaths.SoundDisplayName(_selectedId);
            UseButton.IsEnabled = true;
            PreviewButton.IsEnabled = true;
            Play(_selectedId);
        }
    }

    private static void Play(string? id)
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

    private void Use_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
