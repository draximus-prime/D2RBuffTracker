using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using D2RBuffTracker.Models;
using Wpf.Ui.Controls;

namespace D2RBuffTracker.Views;

public partial class BuffGalleryWindow : FluentWindow
{
    /// <summary>Sentinel returned when the user chooses to create a custom buff.</summary>
    public static readonly BuffCatalogEntry CustomSentinel = new("Custom", DiabloClass.Amazon, 30);

    private BuffCatalogEntry? _result;

    private sealed record ClassFilter(string Label, DiabloClass? Class, Brush Accent);

    private sealed class GalleryItem
    {
        public required BuffCatalogEntry Entry { get; init; }
        public string Name => Entry.Name;
        public string IconPath => Entry.IconRelativePath;
        public required Brush Accent { get; init; }
    }

    private BuffGalleryWindow()
    {
        InitializeComponent();
        BuildClassList();
        Loaded += (_, _) => { ClassList.SelectedIndex = 0; SearchBox.Focus(); };
    }

    public static BuffCatalogEntry? PickBuff(Window? owner)
    {
        var dlg = new BuffGalleryWindow { Owner = owner };
        return dlg.ShowDialog() == true ? dlg._result : null;
    }

    private static Brush AccentBrush(DiabloClass? c)
    {
        var hex = c is { } cls ? DiabloClassInfo.AccentHex(cls) : "#7C4DFF";
        var brush = (Brush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }

    private void BuildClassList()
    {
        var filters = new List<ClassFilter> { new("All classes", null, AccentBrush(null)) };
        filters.AddRange(DiabloClassInfo.All.Select(c => new ClassFilter(c.ToString(), c, AccentBrush(c))));
        ClassList.ItemsSource = filters;
    }

    private void ClassList_OnSelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshItems();

    private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e) => RefreshItems();

    private void RefreshItems()
    {
        if (ClassList.SelectedItem is not ClassFilter filter)
            return;

        var search = SearchBox.Text?.Trim() ?? string.Empty;
        IEnumerable<BuffCatalogEntry> source = filter.Class is { } cls
            ? BuffCatalog.ForClass(cls)
            : BuffCatalog.All;

        if (!string.IsNullOrEmpty(search))
            source = source.Where(b => b.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

        BuffItems.ItemsSource = source
            .OrderBy(b => b.Name)
            .Select(b => new GalleryItem { Entry = b, Accent = AccentBrush(b.Class) })
            .ToList();
    }

    private void BuffTile_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is BuffCatalogEntry entry)
        {
            _result = entry;
            DialogResult = true;
            Close();
        }
    }

    private void Custom_OnClick(object sender, RoutedEventArgs e)
    {
        _result = CustomSentinel;
        DialogResult = true;
        Close();
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
