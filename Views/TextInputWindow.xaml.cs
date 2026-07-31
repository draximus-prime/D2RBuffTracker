using System.Windows;
using Wpf.Ui.Controls;

namespace D2RBuffTracker.Views;

public partial class TextInputWindow : FluentWindow
{
    private TextInputWindow()
    {
        InitializeComponent();
    }

    public static string? Prompt(Window? owner, string title, string label, string initial = "")
    {
        var dlg = new TextInputWindow
        {
            Owner = owner,
            Title = title
        };
        dlg.Bar.Title = title;
        dlg.LabelText.Text = label;
        dlg.Input.Text = initial;
        dlg.Loaded += (_, _) => { dlg.Input.Focus(); dlg.Input.SelectAll(); };
        return dlg.ShowDialog() == true ? dlg.Input.Text : null;
    }

    private void Ok_OnClick(object sender, RoutedEventArgs e)
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
