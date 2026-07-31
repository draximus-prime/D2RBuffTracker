using System.Windows.Forms;

namespace D2RBuffTracker.Services;

/// <summary>
/// Maps Win32 virtual-key codes to friendly display labels and provides a
/// curated list of bindable keys for the fallback key picker.
/// </summary>
public static class KeyNames
{
    public static string ForVirtualKey(int vk)
    {
        var key = (Keys)vk;
        return key switch
        {
            Keys.Oemtilde => "~",
            Keys.OemOpenBrackets => "[",
            Keys.OemCloseBrackets => "]",
            Keys.OemSemicolon => ";",
            Keys.OemQuotes => "'",
            Keys.Oemcomma => ",",
            Keys.OemPeriod => ".",
            Keys.OemMinus => "-",
            Keys.Oemplus => "=",
            Keys.OemQuestion => "/",
            Keys.OemPipe => "\\",
            Keys.D0 => "0",
            Keys.D1 => "1",
            Keys.D2 => "2",
            Keys.D3 => "3",
            Keys.D4 => "4",
            Keys.D5 => "5",
            Keys.D6 => "6",
            Keys.D7 => "7",
            Keys.D8 => "8",
            Keys.D9 => "9",
            Keys.Escape => "Esc",
            Keys.Return => "Enter",
            Keys.Back => "Backspace",
            Keys.Capital => "Caps Lock",
            Keys.LShiftKey => "Left Shift",
            Keys.RShiftKey => "Right Shift",
            Keys.LControlKey => "Left Ctrl",
            Keys.RControlKey => "Right Ctrl",
            Keys.LMenu => "Left Alt",
            Keys.RMenu => "Right Alt",
            _ => key.ToString()
        };
    }
}
