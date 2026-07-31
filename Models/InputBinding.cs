namespace D2RBuffTracker.Models;

/// <summary>
/// The category of physical input a binding refers to.
/// </summary>
public enum InputKind
{
    Keyboard,
    Mouse,
    Gamepad
}

/// <summary>
/// A single, serializable input binding that can represent a keyboard key,
/// a mouse button or a gamepad button. Equality is based on kind + code so
/// captured input events can be matched directly against a stored binding.
/// </summary>
public sealed class InputBinding : IEquatable<InputBinding>
{
    public InputKind Kind { get; set; }

    /// <summary>
    /// Keyboard: the Win32 virtual-key code. Mouse: button index (0-4).
    /// Gamepad: button index.
    /// </summary>
    public int Code { get; set; }

    /// <summary>
    /// Human friendly label shown in the UI, e.g. "F8", "Mouse 2", "Pad 3".
    /// </summary>
    public string Display { get; set; } = string.Empty;

    public InputBinding() { }

    public InputBinding(InputKind kind, int code, string display)
    {
        Kind = kind;
        Code = code;
        Display = display;
    }

    public bool Equals(InputBinding? other)
        => other is not null && other.Kind == Kind && other.Code == Code;

    public override bool Equals(object? obj) => Equals(obj as InputBinding);

    public override int GetHashCode() => HashCode.Combine((int)Kind, Code);

    public override string ToString() => Display;

    public InputBinding Clone() => new(Kind, Code, Display);
}
