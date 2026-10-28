using System;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Hardware.Display;
using Android.OS;
using Android.Views;

namespace RecompOne.SoTN.Android;

internal sealed class CompanionDisplayManager : Java.Lang.Object, DisplayManager.IDisplayListener
{
    private readonly Activity _activity;
    private readonly DisplayManager _displays;
    private readonly Handler _handler = new(Looper.MainLooper!);
    private Presentation? _presentation;
    private bool _started;
    public string Status { get; private set; } = "No second display";

    public CompanionDisplayManager(Activity activity)
    {
        _activity = activity;
        _displays = (DisplayManager)activity.GetSystemService(Context.DisplayService)!;
    }

    public void Start()
    {
        if (!_started) _displays.RegisterDisplayListener(this, _handler);
        _started = true;
        Refresh();
    }

    public void Stop()
    {
        if (_started) _displays.UnregisterDisplayListener(this);
        _started = false;
        ClosePresentation();
    }

    private void ClosePresentation()
    {
        var old = _presentation;
        _presentation = null;
        if (old == null) return;
        old.Dismiss();
        old.Dispose();
    }

    public void Refresh()
    {
        if (!_started) return;
        if (!AndroidSettings.CompanionEnabled)
        {
            ClosePresentation();
            Status = "Off";
            return;
        }
        // Only screens Android explicitly advertises as presentation-compatible.
        // Not every built-in screen exposed by every handheld permits Presentation.
        var display = (_displays.GetDisplays(DisplayManager.DisplayCategoryPresentation) ?? [])
            .FirstOrDefault(d => d.DisplayId != _activity.WindowManager?.DefaultDisplay?.DisplayId);
        if (display == null)
        {
            ClosePresentation();
            Status = "No compatible second display";
            return;
        }
        if (_presentation?.Display?.DisplayId == display.DisplayId && _presentation.IsShowing) return;
        ClosePresentation();
        try
        {
            var p = new Presentation(_activity, display, global::Android.Resource.Style.ThemeMaterialNoActionBarFullscreen);
            _presentation = p;
            p.Window?.AddFlags(WindowManagerFlags.NotFocusable);
            p.SetContentView(new CompanionPanel(p.Context));
            p.Show();
            Status = $"Connected: {display.Name}";
            Console.WriteLine($"[Companion] Presentation on display {display.DisplayId}: {display.Name}");
        }
        catch (Exception ex)
        {
            ClosePresentation();
            Status = "Android cannot use this display";
            Console.Error.WriteLine($"[Companion] Presentation unavailable: {ex.Message}");
        }
    }

    public void OnDisplayAdded(int displayId) => Refresh();
    public void OnDisplayChanged(int displayId) => Refresh();
    public void OnDisplayRemoved(int displayId) => Refresh();

    protected override void Dispose(bool disposing)
    {
        if (disposing) { Stop(); _handler.Dispose(); }
        base.Dispose(disposing);
    }
}
