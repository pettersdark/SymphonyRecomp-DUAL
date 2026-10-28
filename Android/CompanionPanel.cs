using System;
using System.Collections.Generic;
using System.Linq;
using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Android.Widget;
using Sotn;
using Button = Android.Widget.Button;

namespace RecompOne.SoTN.Android;

internal sealed class CompanionPanel : LinearLayout
{
    private readonly TextView _status, _inventory, _description;
    private readonly ExplorationView _map;
    private readonly ScrollView _scroll;
    private readonly LinearLayout _equipmentRows;
    private readonly TextView _actionStatus;
    private readonly Button _confirm;
    private readonly Button _original, _mapTab, _equipmentTab, _relicsTab;
    private readonly LinearLayout _mapContent, _inventoryContent;
    private enum PanelPage { Map, Equipment, Relics }
    private PanelPage _page;
    private bool _choosing;
    private bool _seenMenuOpen;
    private string? _renderedInventory;
    private readonly Action _tick;
    private long _revision = -1;
    private bool _attached;

    public CompanionPanel(Context context) : base(context)
    {
        Orientation = Orientation.Vertical;
        Background = SotnCompanionSkin.Frame(context, SotnSurface.Panel);
        int pad = (int)(12 * (Resources?.DisplayMetrics?.Density ?? 1));
        SetPadding(pad, pad, pad, pad);
        _status = SotnCompanionSkin.Text(context, "", 12);
        _status.SetTextColor(SotnCompanionSkin.Muted);
        _status.SetSingleLine(true);
        _status.Ellipsize = TextUtils.TruncateAt.End;
        _status.SetPadding(0, pad / 3, 0, pad / 2);
        _status.Gravity = GravityFlags.Center;
        AddView(_status);
        var tabs = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        var mapTab = SotnCompanionSkin.Button(context, "MAP", SotnSurface.Tab);
        var inventoryTab = SotnCompanionSkin.Button(context, "EQUIPMENT", SotnSurface.Tab);
        var relicsTab = SotnCompanionSkin.Button(context, "RELICS", SotnSurface.Tab);
        _mapTab = mapTab; _equipmentTab = inventoryTab; _relicsTab = relicsTab;
        foreach (var tab in new[] { mapTab, inventoryTab, relicsTab })
        {
            tab.Gravity = GravityFlags.Center;
            tab.LetterSpacing = .1f;
            tab.Typeface = Typeface.Create(Typeface.Monospace, TypefaceStyle.Bold);
        }
        mapTab.Selected = true;
        tabs.AddView(mapTab, new LayoutParams(0, LayoutParams.WrapContent, 1) { RightMargin = pad / 4 });
        tabs.AddView(inventoryTab, new LayoutParams(0, LayoutParams.WrapContent, 1) { LeftMargin = pad / 4 });
        tabs.AddView(relicsTab, new LayoutParams(0, LayoutParams.WrapContent, 1) { LeftMargin = pad / 2 });
        AddView(tabs, new LayoutParams(LayoutParams.MatchParent, LayoutParams.WrapContent) { BottomMargin = pad / 2 });
        _map = new ExplorationView(context);
        _inventory = SotnCompanionSkin.Text(context, "", 14);
        _inventory.SetLineSpacing(pad / 3, 1);
        _inventory.SetPadding(pad / 2, pad / 2, pad / 2, pad);
        _scroll = new ScrollView(context) { Visibility = ViewStates.Gone };
        var inventoryContent = new LinearLayout(context) { Orientation = Orientation.Vertical };
        _inventoryContent = inventoryContent;
        inventoryContent.Visibility = ViewStates.Gone;
        var original = SotnCompanionSkin.Button(context, "Open Equipment");
        _original = original;
        original.Click += (_, _) => ResetSelection();
        var resume = SotnCompanionSkin.Button(context, "Resume Game");
        resume.Click += (_, _) => ShowPage(PanelPage.Map);
        AddPair(inventoryContent, original, resume);
        _actionStatus = SotnCompanionSkin.Text(context, "", 12, true);
        _actionStatus.SetPadding(pad / 3, pad / 3, pad / 3, pad / 3);
        _actionStatus.SetMaxLines(2);
        _actionStatus.Ellipsize = TextUtils.TruncateAt.End;
        inventoryContent.AddView(_actionStatus);
        _confirm = SotnCompanionSkin.Button(context, "Confirm Equipment");
        _confirm.Enabled = false;
        _confirm.Click += (_, _) => CompanionEquipment.Confirm();
        var back = SotnCompanionSkin.Button(context, "Back");
        back.Click += (_, _) =>
        {
            // Native Equip stays in its item picker after confirmation. Back must
            // return to its overview before a second Back resumes gameplay.
            bool originalPicker = _page == PanelPage.Equipment && CompanionState.Latest.MenuStep is 0x51 or 0x61 or 0x41;
            if (_choosing || CompanionEquipment.Feedback.CanConfirm || originalPicker) ResetSelection();
            else ShowPage(PanelPage.Map);
        };
        AddPair(inventoryContent, _confirm, back);
        _equipmentRows = new LinearLayout(context) { Orientation = Orientation.Vertical };
        _scroll.Visibility = ViewStates.Visible;
        _scroll.SetPadding(0, pad / 3, pad / 3, 0);
        _scroll.AddView(_equipmentRows);
        inventoryContent.AddView(_scroll, new LayoutParams(LayoutParams.MatchParent, 0, 1));
        _description = SotnCompanionSkin.Text(context, "Select a slot or item to prepare equipment.\nConfirm while the original menu is paused.", 13);
        _description.SetMinLines(2);
        _description.SetPadding(pad, pad / 2, pad, pad / 2);
        _description.SetTextColor(SotnCompanionSkin.Muted);
        _description.Background = SotnCompanionSkin.Frame(context, SotnSurface.Inset);
        inventoryContent.AddView(_description);
        var mapContent = new LinearLayout(context) { Orientation = Orientation.Vertical };
        _mapContent = mapContent;
        var mapTools = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        var legend = SotnCompanionSkin.Text(context, "EXPLORED AREAS", 11, true);
        legend.Gravity = GravityFlags.CenterVertical;
        mapTools.AddView(legend, new LayoutParams(0, LayoutParams.MatchParent, 1));
        var zoom = SotnCompanionSkin.Button(context, "Full Map");
        zoom.Click += (_, _) => { _map.WholeCastle = !_map.WholeCastle; zoom.Text = _map.WholeCastle ? "Zoom to Route" : "Full Map"; _map.Invalidate(); };
        mapTools.AddView(zoom);
        mapContent.AddView(mapTools, new LayoutParams(LayoutParams.MatchParent, LayoutParams.WrapContent) { BottomMargin = pad / 2 });
        var mapFrame = new FrameLayout(context) { Background = SotnCompanionSkin.Frame(context, SotnSurface.Inset) };
        mapFrame.SetPadding(pad / 2, pad / 2, pad / 2, pad / 2);
        mapFrame.AddView(_map, new FrameLayout.LayoutParams(LayoutParams.MatchParent, LayoutParams.MatchParent));
        mapContent.AddView(mapFrame, new LayoutParams(LayoutParams.MatchParent, 0, 1));
        AddView(mapContent, new LayoutParams(LayoutParams.MatchParent, 0, 1));
        AddView(inventoryContent, new LayoutParams(LayoutParams.MatchParent, 0, 1));
        mapTab.Click += (_, _) => ShowPage(PanelPage.Map);
        inventoryTab.Click += (_, _) => ShowPage(PanelPage.Equipment);
        relicsTab.Click += (_, _) => ShowPage(PanelPage.Relics);
        _tick = Tick;
    }

    private void ShowPage(PanelPage page)
    {
        _page = page;
        _mapTab.Selected = page == PanelPage.Map;
        _equipmentTab.Selected = page == PanelPage.Equipment;
        _relicsTab.Selected = page == PanelPage.Relics;
        _mapContent.Visibility = page == PanelPage.Map ? ViewStates.Visible : ViewStates.Gone;
        _inventoryContent.Visibility = page == PanelPage.Map ? ViewStates.Gone : ViewStates.Visible;
        if (page == PanelPage.Map)
        {
            _choosing = false;
            _confirm.Enabled = false;
            CompanionEquipment.Close();
        }
        else ResetSelection();
    }

    private void ResetSelection()
    {
        _choosing = false;
        _seenMenuOpen = false;
        bool relics = _page == PanelPage.Relics;
        _original.Text = relics ? "Open Relics" : "Open Equipment";
        _confirm.Text = relics ? "Toggle Relic" : "Confirm Equipment";
        _confirm.Enabled = false;
        _description.Text = relics ? "Select a relic to review it in the original menu.\nToggle Relic changes its active state while paused."
            : "Select a slot or item to prepare equipment.\nReview it above, then confirm while paused.";
        RenderEquipment(CompanionState.Latest);
        _scroll.ScrollTo(0, 0);
        if (relics) CompanionEquipment.OpenRelics(); else CompanionEquipment.OpenEquipment();
    }

    protected override void OnAttachedToWindow()
    {
        base.OnAttachedToWindow();
        _attached = true;
        Tick();
    }

    protected override void OnDetachedFromWindow()
    {
        _attached = false;
        RemoveCallbacks(_tick);
        base.OnDetachedFromWindow();
    }

    private void Tick()
    {
        if (!_attached) return;
        var state = CompanionState.Latest;
        if (state.Revision != _revision)
        {
            _revision = state.Revision;
            _status.Text = state.Status;
            if (_renderedInventory != state.InventoryText)
            {
                _renderedInventory = state.InventoryText;
                if (!_choosing) RenderEquipment(state);
            }
            _map.Snapshot = state;
            _map.Invalidate();
        }
        var feedback = CompanionEquipment.Feedback;
        // A manual START/Back above must not leave a stale chooser below.
        if (state.MenuOpen) _seenMenuOpen = true;
        if (_page != PanelPage.Map && state.Playing && _seenMenuOpen && !feedback.Busy && !state.MenuOpen)
        {
            _page = PanelPage.Map; _choosing = false;
            _mapTab.Selected = true; _equipmentTab.Selected = _relicsTab.Selected = false;
            _mapContent.Visibility = ViewStates.Visible; _inventoryContent.Visibility = ViewStates.Gone;
        }
        if (_actionStatus.Text != feedback.Message) _actionStatus.Text = feedback.Message;
        _confirm.Enabled = feedback.CanConfirm && !feedback.Busy && state.Alucard && _page != PanelPage.Map;
        _confirm.Selected = _confirm.Enabled;
        PostDelayed(_tick, 200);
    }

    // Retail US menu labels slot0 as Right hand, despite the wrapper enum's name.
    private static readonly string[] SlotLabels = ["Right hand", "Left hand", "Head", "Armor", "Cloak", "Accessory 1", "Accessory 2"];
    private void RenderEquipment(CompanionSnapshot state)
    {
        _equipmentRows.RemoveAllViews();
        if (_page == PanelPage.Relics)
        {
            _equipmentRows.AddView(SotnCompanionSkin.Heading(Context, "OBTAINED RELICS"));
            var buttons = new List<Button>();
            foreach (var relic in state.Relics ?? [])
            {
                var button = SotnCompanionSkin.Button(Context, $"{relic.Name}\n{(relic.Active ? "ON" : "OFF")}", SotnSurface.Item);
                button.SetMinHeight(SotnCompanionSkin.Dp(Context, 64));
                button.Click += (_, _) =>
                {
                    if (CompanionEquipment.Feedback.Busy) return;
                    _description.Text = $"Selected: {relic.Name}\nReview above; Toggle Relic applies the change.";
                    CompanionEquipment.SelectRelic(relic.Id);
                };
                buttons.Add(button);
            }
            AddGrid(buttons);
            if (buttons.Count == 0) _equipmentRows.AddView(SotnCompanionSkin.Text(Context,
                state.Alucard ? "No relics obtained yet." : "Relics are available when playing as Alucard.", 14));
            return;
        }
        if (!state.Alucard || state.Equipment is not { Length: 7 })
        { _inventory.Text = state.InventoryText; _equipmentRows.AddView(_inventory); return; }
        _equipmentRows.AddView(SotnCompanionSkin.Heading(Context, "CURRENT EQUIPMENT"));
        var equipmentButtons = new List<Button>();
        for (int i = 0; i < 7; i++)
        {
            int slot = i, id = state.Equipment[i];
            string name = slot < 2 ? CompanionState.Name<HandItem>(id) : CompanionState.Name<BodyItem>(id);
            var button = ItemButton($"{SlotLabels[slot]}\n{name}", slot < 2, id);
            button.Click += (_, _) => ChooseForSlot(slot);
            equipmentButtons.Add(button);
        }
        AddGrid(equipmentButtons);
        var heading = SotnCompanionSkin.Heading(Context, "INVENTORY · AVAILABLE ITEMS");
        _equipmentRows.AddView(heading);
        var bagButtons = new List<Button>();
        foreach (var item in state.Items ?? [])
        {
            if (item.BagCount <= 0) continue;
            var button = ItemButton($"{item.Name}  ×{item.BagCount}", item.Hand, item.Id);
            button.Click += (_, _) => ChooseSlot(item);
            bagButtons.Add(button);
        }
        AddGrid(bagButtons);
        if (bagButtons.Count == 0) _equipmentRows.AddView(SotnCompanionSkin.Text(Context, "Inventory is empty.", 14));
        int spells = state.InventoryText.IndexOf("\nLEARNED SPELLS", StringComparison.Ordinal);
        _inventory.Text = spells >= 0 ? state.InventoryText[(spells + 1)..] : "";
        _equipmentRows.AddView(_inventory);
    }
    private static void AddPair(LinearLayout parent, View first, View? second)
    {
        int gap = SotnCompanionSkin.Dp(parent.Context, 3);
        var row = new LinearLayout(parent.Context) { Orientation = Orientation.Horizontal };
        row.AddView(first, new LayoutParams(0, LayoutParams.WrapContent, 1) { RightMargin = gap });
        row.AddView(second ?? new View(parent.Context), new LayoutParams(0, LayoutParams.WrapContent, 1) { LeftMargin = gap });
        parent.AddView(row, new LayoutParams(LayoutParams.MatchParent, LayoutParams.WrapContent) { BottomMargin = gap * 2 });
    }
    private void AddGrid(List<Button> buttons)
    {
        for (int i = 0; i < buttons.Count; i += 2)
            AddPair(_equipmentRows, buttons[i], i + 1 < buttons.Count ? buttons[i + 1] : null);
    }
    private readonly Dictionary<string, Bitmap> _icons = new();
    private readonly HashSet<string> _missingIcons = new();
    private Button ItemButton(string text, bool hand, int id)
    {
        var button = SotnCompanionSkin.Button(Context, text, SotnSurface.Item);
        button.SetMinHeight(SotnCompanionSkin.Dp(Context, 64));
        button.SetMinimumHeight(SotnCompanionSkin.Dp(Context, 64));
        button.SetMaxLines(2);
        button.Ellipsize = TextUtils.TruncateAt.End;
        int split = text.IndexOf('\n');
        if (split >= 0)
        {
            var styled = new SpannableString(text);
            styled.SetSpan(new RelativeSizeSpan(.78f), 0, split, SpanTypes.ExclusiveExclusive);
            styled.SetSpan(new ForegroundColorSpan(SotnCompanionSkin.Muted), 0, split, SpanTypes.ExclusiveExclusive);
            styled.SetSpan(new StyleSpan(TypefaceStyle.Bold), split + 1, text.Length, SpanTypes.ExclusiveExclusive);
            button.TextFormatted = styled;
        }
        string key = hand ? ((HandItem)id).ToString() : ((BodyItem)id).ToString();
        if (!_missingIcons.Contains(key) && !_icons.ContainsKey(key))
        {
            try
            {
                using var stream = Context.Assets!.Open($"tracker/{key}.png");
                var bitmap = BitmapFactory.DecodeStream(stream);
                if (bitmap != null) _icons.Add(key, bitmap); else _missingIcons.Add(key);
            }
            catch (Java.IO.FileNotFoundException) { _missingIcons.Add(key); }
            catch (System.IO.IOException) { _missingIcons.Add(key); }
        }
        if (_icons.TryGetValue(key, out var icon))
        {
            var drawable = new global::Android.Graphics.Drawables.BitmapDrawable(Resources, icon);
            drawable.SetFilterBitmap(false);
            int size = SotnCompanionSkin.Dp(Context, 30);
            drawable.SetBounds(0, 0, size, size);
            button.SetCompoundDrawables(drawable, null, null, null);
            button.CompoundDrawablePadding = SotnCompanionSkin.Dp(Context, 10);
        }
        return button;
    }
    private void ChooseForSlot(int slot)
    {
        if (CompanionEquipment.Feedback.Busy) return;
        var state = CompanionState.Latest;
        if (!state.Alucard) return;
        _description.Text = $"{SlotLabels[slot]} · select an item.\nReview the description and ATK/DEF in the original menu.";
        CompanionEquipment.OpenEquipment();
        var choices = (state.Items ?? []).Where(i => i.BagCount > 0 && i.Hand == (slot < 2) && CompanionEquipment.Compatible(slot, i.Id)).ToArray();
        var names = new[] { "Unequip this slot" }.Concat(choices.Select(i => $"{i.Name} ×{i.BagCount}")).ToArray();
        ShowChoices(SlotLabels[slot], names, index => CompanionEquipment.Prepare(slot,
            index == 0 ? CompanionEquipment.EmptyForSlot(slot) : choices[index - 1].Id));
    }
    private void ChooseSlot(CompanionItem item)
    {
        if (CompanionEquipment.Feedback.Busy) return;
        _description.Text = $"{item.Name} · In inventory: {item.BagCount}\nPrepare equipment and confirm while the original menu is paused.";
        int[] slots = Enumerable.Range(0, 7).Where(s => (s < 2) == item.Hand && CompanionEquipment.Compatible(s, item.Id)).ToArray();
        if (slots.Length == 1) { CompanionEquipment.Prepare(slots[0], item.Id); return; }
        CompanionEquipment.OpenEquipment();
        ShowChoices($"Prepare {item.Name}", slots.Select(s => SlotLabels[s]).ToArray(), index => CompanionEquipment.Prepare(slots[index], item.Id));
    }
    private void ShowChoices(string title, string[] names, Action<int> choose)
    {
        _choosing = true;
        // Keep choices in the Presentation's own view tree. An ordinary AlertDialog
        // has application window type2, which is invalid for a presentation context2037.
        _equipmentRows.RemoveAllViews();
        var heading = SotnCompanionSkin.Heading(Context, title);
        _equipmentRows.AddView(heading);
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var button = SotnCompanionSkin.Button(Context, names[i]);
            button.Click += (_, _) =>
            {
                choose(index);
                _choosing = false;
                RenderEquipment(CompanionState.Latest);
                _scroll.SmoothScrollTo(0, 0);
            };
            _equipmentRows.AddView(button, new LayoutParams(LayoutParams.MatchParent, LayoutParams.WrapContent)
                { BottomMargin = SotnCompanionSkin.Dp(Context, 6) });
        }
        var cancel = SotnCompanionSkin.Button(Context, "Cancel Selection");
        cancel.Click += (_, _) => ResetSelection();
        _equipmentRows.AddView(cancel);
        _scroll.SmoothScrollTo(0, 0);
    }
}

internal sealed class ExplorationView(Context context) : View(context)
{
    public bool WholeCastle { get; set; }
    public CompanionSnapshot Snapshot { get; set; } = CompanionState.Latest;
    private readonly Paint _paint = new() { AntiAlias = false };
    private readonly global::Android.Graphics.Path _marker = new();

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        var s = Snapshot;
        int count = 0, minX = 64, minY = 64, maxX = 0, maxY = 0;
        canvas.DrawColor(SotnCompanionSkin.Ink);
        // Fixed coordinates for both castles. No blueprint asset is drawn: unknown cells
        // stay black, including purchased-but-unvisited cells and undiscovered secrets.
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
                if (CastleExploration.IsVisited(s.Exploration, s.Castle, x, y))
                { count++; minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
        int span = WholeCastle || count == 0 ? 64 : Math.Min(64, Math.Max(16, Math.Max(maxX - minX, maxY - minY) + 5));
        int originX = WholeCastle || count == 0 ? 0 : Math.Clamp((minX + maxX - span + 1) / 2, 0, 64 - span);
        int originY = WholeCastle || count == 0 ? 0 : Math.Clamp((minY + maxY - span + 1) / 2, 0, 64 - span);
        float density = Resources?.DisplayMetrics?.Density ?? 1;
        float footer = 30 * density, padding = 8 * density;
        float availableHeight = Math.Max(0, Height - footer);
        float size = Math.Max(0, Math.Min((Width - 2 * padding) / span, (availableHeight - 2 * padding) / span));
        float left = (Width - size * span) / 2 - originX * size;
        float top = (availableHeight - size * span) / 2 - originY * size;
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                if (!CastleExploration.IsVisited(s.Exploration, s.Castle, x, y)) continue;
                _paint.Color = Color.Rgb(41, 79, 164);
                canvas.DrawRect(left + x * size, top + y * size,
                    left + (x + 1) * size, top + (y + 1) * size, _paint);
                _paint.Color = Color.Rgb(153, 185, 240);
                _paint.StrokeWidth = 1;
                float l = left + x * size, t = top + y * size;
                if (!CastleExploration.IsVisited(s.Exploration, s.Castle, x - 1, y)) canvas.DrawLine(l, t, l, t + size, _paint);
                if (!CastleExploration.IsVisited(s.Exploration, s.Castle, x + 1, y)) canvas.DrawLine(l + size, t, l + size, t + size, _paint);
                if (!CastleExploration.IsVisited(s.Exploration, s.Castle, x, y - 1)) canvas.DrawLine(l, t, l + size, t, _paint);
                if (!CastleExploration.IsVisited(s.Exploration, s.Castle, x, y + 1)) canvas.DrawLine(l, t + size, l + size, t + size, _paint);
            }
        if (s.X >= 0 && s.Y >= 0)
        {
            float cx = left + (s.X + .5f) * size, cy = top + (s.Y + .5f) * size;
            float radius = Math.Max(2 * density, size * .28f);
            _marker.Reset();
            _marker.MoveTo(cx, cy - radius);
            _marker.LineTo(cx + radius, cy);
            _marker.LineTo(cx, cy + radius);
            _marker.LineTo(cx - radius, cy);
            _marker.Close();
            _paint.SetStyle(Paint.Style.Stroke);
            _paint.StrokeWidth = 2 * density;
            _paint.Color = Color.Black;
            canvas.DrawPath(_marker, _paint);
            _paint.SetStyle(Paint.Style.Fill);
            _paint.Color = SotnCompanionSkin.Gold;
            canvas.DrawPath(_marker, _paint);
        }
        _paint.Color = Color.Rgb(43, 57, 91);
        _paint.StrokeWidth = density;
        canvas.DrawLine(8 * density, Height - footer, Width - 8 * density, Height - footer, _paint);
        _paint.Color = SotnCompanionSkin.Muted;
        _paint.SetTypeface(Typeface.Monospace);
        _paint.TextSize = 12 * (Resources?.DisplayMetrics?.ScaledDensity ?? 1);
        canvas.DrawText(count == 0 ? "No areas explored yet" : $"{count} cells explored · Position: gold", 8 * density, Height - 10 * density, _paint);
        ContentDescription = $"Map of {(s.Castle == 0 ? "normal castle" : "inverted castle")}: {count} explored cells";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _paint.Dispose(); _marker.Dispose(); }
        base.Dispose(disposing);
    }
}
