using System;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using Button = Android.Widget.Button;

namespace RecompOne.SoTN.Android;

internal static class SotnCompanionSkin
{
    public static readonly Color Ink = Color.Rgb(3, 7, 34);
    public static readonly Color Silver = Color.Rgb(213, 221, 240);
    public static readonly Color White = Color.Rgb(247, 244, 233);
    public static readonly Color Gold = Color.Rgb(223, 194, 129);
    public static readonly Color Muted = Color.Rgb(172, 189, 227);
    public static int Dp(Context context, float value) => (int)(value * (context.Resources?.DisplayMetrics?.Density ?? 1));

    public static TextView Text(Context context, string text, float size = 15, bool gold = false)
    {
        var view = new TextView(context) { Text = text, TextSize = size, Typeface = Typeface.Create("serif", TypefaceStyle.Bold) };
        view.SetTextColor(gold ? Gold : White);
        view.SetShadowLayer(Dp(context, 1), Dp(context, 1), Dp(context, 1), Color.Black);
        return view;
    }

    public static TextView Heading(Context context, string text)
    {
        var view = Text(context, text, 13, true);
        view.Background = Frame(context, SotnSurface.Heading);
        view.SetPadding(Dp(context, 12), Dp(context, 7), Dp(context, 8), Dp(context, 7));
        view.Typeface = Typeface.Create("serif", TypefaceStyle.Bold);
        view.LetterSpacing = .06f;
        return view;
    }

    public static SotnFrameDrawable Frame(Context context, SotnSurface surface, bool selected = false, bool disabled = false) =>
        new(surface, selected, disabled, context.Resources?.DisplayMetrics?.Density ?? 1);

    public static Button Button(Context context, string text, SotnSurface surface = SotnSurface.Control)
    {
        var button = new Button(context) { Text = text, TextSize = 15, Typeface = Typeface.Create("serif", TypefaceStyle.Bold),
            Gravity = GravityFlags.CenterVertical | GravityFlags.Left, Elevation = 0 };
        button.SetAllCaps(false);
        button.BackgroundTintList = null;
        button.StateListAnimator = null;
        button.SetMinHeight(Dp(context, 42));
        button.SetMinimumHeight(Dp(context, 42));
        button.SetMinWidth(0);
        button.SetMinimumWidth(0);
        button.SetPadding(Dp(context, 16), Dp(context, 6), Dp(context, 12), Dp(context, 6));
        button.SetShadowLayer(Dp(context, 1), Dp(context, 1), Dp(context, 1), Color.Black);
        var states = new StateListDrawable();
        states.AddState([-global::Android.Resource.Attribute.StateEnabled], Frame(context, surface, disabled: true));
        states.AddState([global::Android.Resource.Attribute.StatePressed], Frame(context, surface, true));
        states.AddState([global::Android.Resource.Attribute.StateSelected], Frame(context, surface, true));
        states.AddState([global::Android.Resource.Attribute.StateFocused], Frame(context, surface, true));
        states.AddState([], Frame(context, surface));
        button.Background = states;
        button.SetTextColor(new global::Android.Content.Res.ColorStateList(
            [new[] { -global::Android.Resource.Attribute.StateEnabled }, Array.Empty<int>()],
            [Color.Rgb(119, 132, 164).ToArgb(), White.ToArgb()]));
        return button;
    }
}

internal enum SotnSurface { Panel, Control, Tab, Item, Inset, Heading }

// Cobalt panels, silver bevels and a gold cursor echo the native equipment menu.
// No bitmap backgrounds, animation timers, or changes to the game's renderer.
internal sealed class SotnFrameDrawable(SotnSurface surface, bool selected, bool disabled, float density) : Drawable
{
    private readonly Paint _paint = new() { AntiAlias = false };
    private LinearGradient? _gradient;
    protected override void OnBoundsChange(Rect bounds)
    {
        base.OnBoundsChange(bounds);
        _gradient?.Dispose();
        var top = surface == SotnSurface.Panel ? Color.Rgb(19, 30, 121) :
            surface == SotnSurface.Inset ? InkColor : Color.Rgb(19, 34, 126);
        var bottom = surface == SotnSurface.Panel ? Color.Rgb(5, 10, 64) : Color.Rgb(8, 16, 83);
        if (disabled) { top = Color.Rgb(12, 21, 44); bottom = Color.Rgb(8, 14, 33); }
        if (selected) { top = Color.Rgb(39, 67, 174); bottom = Color.Rgb(15, 30, 107); }
        _gradient = new LinearGradient(0, bounds.Top, 0, Math.Max(bounds.Top + 1, bounds.Bottom), top, bottom, Shader.TileMode.Clamp!);
    }
    private static Color InkColor => SotnCompanionSkin.Ink;
    public override void Draw(Canvas canvas)
    {
        var b = Bounds;
        _paint.SetStyle(Paint.Style.Fill);
        _paint.SetShader(_gradient);
        canvas.DrawRect(b, _paint);
        _paint.SetShader(null);
        float d = Math.Max(1, density);
        // Texture belongs to the outer panel, leaving text and item cards calm.
        if (surface == SotnSurface.Panel)
        {
            _paint.Color = Color.Argb(8, 155, 178, 227);
            _paint.StrokeWidth = 1;
            for (float y = b.Top + 5 * d; y < b.Bottom - 5 * d; y += 5 * d)
                canvas.DrawLine(b.Left + 4 * d, y, b.Right - 4 * d, y, _paint);
        }
        _paint.SetStyle(Paint.Style.Stroke);
        _paint.StrokeWidth = d;
        _paint.Color = Color.Black;
        canvas.DrawRect(b.Left + .5f * d, b.Top + .5f * d, b.Right - .5f * d, b.Bottom - .5f * d, _paint);
        _paint.Color = selected ? SotnCompanionSkin.Gold :
            surface == SotnSurface.Item ? Color.Rgb(93, 113, 175) : SotnCompanionSkin.Silver;
        if (disabled) _paint.Color = Color.Rgb(39, 51, 77);
        canvas.DrawRect(b.Left + 1.5f * d, b.Top + 1.5f * d, b.Right - 1.5f * d, b.Bottom - 1.5f * d, _paint);
        _paint.Color = disabled ? Color.Rgb(27, 38, 60) : Color.Argb(100, 154, 178, 219);
        canvas.DrawLine(b.Left + 3 * d, b.Top + 3 * d, b.Right - 3 * d, b.Top + 3 * d, _paint);
        if (surface == SotnSurface.Panel)
        {
            _paint.Color = Color.Rgb(46, 63, 105);
            canvas.DrawRect(b.Left + 4.5f * d, b.Top + 4.5f * d, b.Right - 4.5f * d, b.Bottom - 4.5f * d, _paint);
            _paint.Color = SotnCompanionSkin.Gold;
            // Short corner inlays echo the original menu without filling the UI with ornament.
            foreach (float x in new[] { b.Left + 5 * d, b.Right - 5 * d })
                foreach (float y in new[] { b.Top + 5 * d, b.Bottom - 5 * d })
                {
                    float sx = x < b.CenterX() ? 1 : -1, sy = y < b.CenterY() ? 1 : -1;
                    canvas.DrawLine(x, y, x + sx * 11 * d, y, _paint);
                    canvas.DrawLine(x, y, x, y + sy * 11 * d, _paint);
                }
        }
        if (surface == SotnSurface.Heading)
        {
            _paint.Color = SotnCompanionSkin.Gold;
            canvas.DrawLine(b.Left + 8 * d, b.Bottom - 2 * d, b.Right - 8 * d, b.Bottom - 2 * d, _paint);
        }
        if (selected)
        {
            _paint.SetStyle(Paint.Style.Fill);
            _paint.Color = SotnCompanionSkin.Gold;
            float x = b.Left + 7 * d, y = b.ExactCenterY();
            canvas.DrawRect(x, y - 3 * d, x + 2 * d, y + 3 * d, _paint);
            canvas.DrawRect(x + 2 * d, y - 2 * d, x + 4 * d, y + 2 * d, _paint);
            canvas.DrawRect(x + 4 * d, y - d, x + 5 * d, y + d, _paint);
        }
        if (surface == SotnSurface.Tab && selected)
        {
            _paint.SetStyle(Paint.Style.Fill);
            _paint.Color = SotnCompanionSkin.Gold;
            canvas.DrawRect(b.Left + 3 * d, b.Bottom - 4 * d, b.Right - 3 * d, b.Bottom - 2 * d, _paint);
        }
    }
    public override void SetAlpha(int alpha) { _paint.Alpha = alpha; InvalidateSelf(); }
    public override void SetColorFilter(ColorFilter? colorFilter) => _paint.SetColorFilter(colorFilter);
    public override int Opacity => (int)Format.Opaque;
    protected override void Dispose(bool disposing)
    {
        if (disposing) { _gradient?.Dispose(); _paint.Dispose(); }
        base.Dispose(disposing);
    }
}
