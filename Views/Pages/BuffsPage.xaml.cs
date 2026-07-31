using System.Windows;
using System.Windows.Controls;
using D2RBuffTracker.Models;
using D2RBuffTracker.ViewModels;
using Wpf.Ui.Controls;

namespace D2RBuffTracker.Views;

public partial class BuffsPage : UserControl
{
    public BuffsPage()
    {
        InitializeComponent();
    }

    private MainViewModel Vm => (MainViewModel)DataContext;

    private void NewProfile_OnClick(object sender, RoutedEventArgs e)
    {
        var name = TextInputWindow.Prompt(Window.GetWindow(this), "New profile", "Profile name", "New Profile");
        if (!string.IsNullOrWhiteSpace(name))
            Vm.AddProfile(name.Trim());
    }

    private void RenameProfile_OnClick(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedProfile is not { } profile)
            return;
        var name = TextInputWindow.Prompt(Window.GetWindow(this), "Rename profile", "Profile name", profile.Name);
        if (!string.IsNullOrWhiteSpace(name))
            Vm.RenameProfile(profile, name.Trim());
    }

    private void DeleteProfile_OnClick(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedProfile is not { } profile)
            return;
        var result = System.Windows.MessageBox.Show(
            Window.GetWindow(this)!,
            $"Delete the profile \"{profile.Name}\" and all of its buffs?",
            "Delete profile", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result == System.Windows.MessageBoxResult.Yes)
            Vm.DeleteProfile(profile);
    }

    private void AddBuff_OnClick(object sender, RoutedEventArgs e)
    {
        var entry = BuffGalleryWindow.PickBuff(Window.GetWindow(this));
        if (entry == null)
            return;

        var buff = entry == BuffGalleryWindow.CustomSentinel
            ? Vm.AddCustomBuff()
            : Vm.AddBuffFromCatalog(entry);

        // Open the editor immediately so the user can bind keys.
        OpenEditor(buff);
    }

    private void EditBuff_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is TrackedBuff buff)
            OpenEditor(buff);
    }

    private void OpenEditor(TrackedBuff buff)
    {
        var edited = BuffEditorWindow.Edit(Window.GetWindow(this), buff);
        if (edited)
            Vm.SaveBuffs();
    }

    private void DeleteBuff_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not TrackedBuff buff)
            return;
        var result = System.Windows.MessageBox.Show(
            Window.GetWindow(this)!,
            $"Remove \"{buff.Name}\" from this profile?",
            "Remove buff", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
        if (result == System.Windows.MessageBoxResult.Yes)
            Vm.RemoveBuff(buff);
    }

    private void Enable_OnClick(object sender, RoutedEventArgs e) => Vm.SaveBuffs();
}
