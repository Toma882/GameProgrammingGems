namespace GPGems.Core.Graphics;

/// <summary>
/// 独立于 UI 框架的 RGB 颜色结构
/// </summary>
public readonly struct RgbColor(byte r, byte g, byte b)
{
    public byte R { get; } = r;
    public byte G { get; } = g;
    public byte B { get; } = b;

    public static RgbColor FromRgb(byte r, byte g, byte b) => new(r, g, b);

    public static implicit operator RgbColor((byte r, byte g, byte b) tuple) =>
        new(tuple.r, tuple.g, tuple.b);
}
