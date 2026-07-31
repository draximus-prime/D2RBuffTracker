using System.Reflection;
using System.Windows.Controls;

namespace D2RBuffTracker.Views;

public partial class AboutPage : UserControl
{
    public AboutPage()
    {
        InitializeComponent();

        // Show the real build version so the About page can never drift out of
        // sync with the release. Prefer the informational version (full semver,
        // e.g. "1.3.0" or "0.0.0-dev"); fall back to the numeric assembly version.
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        // The SDK may append "+<commit>" build metadata; drop it for display.
        var version = info?.Split('+', 2)[0];
        if (string.IsNullOrWhiteSpace(version))
            version = asm.GetName().Version?.ToString(3);

        VersionText.Text = $"Version {version ?? "0.0.0"}";
    }
}
