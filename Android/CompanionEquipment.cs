using System;
using System.Diagnostics;
using System.Threading;
using RecompOne.Runtime.Hardware;
using Sotn;

namespace RecompOne.SoTN.Android;

internal sealed record EquipmentFeedback(string Message, bool Busy = false, bool CanConfirm = false);

// UI threads only enqueue requests. Navigation, cursor selection and validation happen
// at the game's VSync boundary. Equipment/counts/stats are NEVER written by this bridge.
internal static class CompanionEquipment
{
    private enum Command { Open, OpenEquipment, OpenRelics, Prepare, SelectRelic, Confirm, Back, Close }
    private sealed record Request(Command Command, int Slot = -1, int Id = -1);
    private static readonly object Gate = new();
    private static Request? _pending, _active, _candidate;
    private static bool _cancel;
    private static int _cooldown, _frames, _verify;
    private static bool _exitSent, _relicWasActive;
    private static long _started;
    private static EquipmentFeedback _feedback = new("Select a slot or item to open the original equipment menu.");
    public static EquipmentFeedback Feedback => Volatile.Read(ref _feedback);
    public const uint MenuStepAddress = 0x800978F8;
    private const uint MappingAddress = 0x801375D8;
    private const uint ScrollAddress = 0x8003C9C8;
    private const int Root = 0x10, Equip = 0x40, Hand = 0x51, Body = 0x61;

    public static void OpenOriginal() => Enqueue(new(Command.Open));
    public static void OpenEquipment() => Enqueue(new(Command.OpenEquipment));
    public static void OpenRelics() => Enqueue(new(Command.OpenRelics));
    public static void SelectRelic(int id) => Enqueue(new(Command.SelectRelic, Id: id));
    public static void Prepare(int slot, int id) => Enqueue(new(Command.Prepare, slot, id));
    public static void Confirm() => Enqueue(new(Command.Confirm));
    public static void Back() => Enqueue(new(Command.Back));
    public static void Close() => Enqueue(new(Command.Close));
    public static void CancelPending() { lock (Gate) { _cancel = true; _pending = null; } Release(); }
    private static void Enqueue(Request request)
    {
        lock (Gate)
        {
            // A tab, Back or Resume replaces navigation already in progress. Never
            // leave a previous candidate confirmable while changing the upper menu.
            if (request.Command == Command.Confirm && (Feedback.Busy || !Feedback.CanConfirm)) return;
            _pending = request;
            Say("Opening original menu…", busy: true);
        }
    }
    private static void Say(string text, bool busy = false, bool confirm = false) =>
        Volatile.Write(ref _feedback, new(text, busy, confirm));
    private static void Release() { Controller.MenuInput = 0xFFFF; Controller.MenuOwnsInput = false; }
    private static void Finish(string message, bool ready = false)
    {
        lock (Gate) _active = null;
        _cooldown = _verify = 0;
        Release();
        if (!ready) _candidate = null;
        Say(message, confirm: ready);
        Console.WriteLine($"[TouchEquip] {message}");
    }
    private static void Pulse(ushort button)
    {
        Controller.MenuOwnsInput = true;
        Controller.MenuInput = unchecked((ushort)~button);
        _cooldown = 10; // four held frames, six neutral frames; no repeat or double-tap
    }
    public static int EmptyForSlot(int slot) => slot switch
    {
        0 or 1 => 0, 2 => Inventory.HeadNone, 3 => Inventory.ArmorNone,
        4 => Inventory.CapeNone, 5 or 6 => Inventory.AccessoryNone, _ => -1
    };
    public static bool Compatible(int slot, int id)
    {
        if ((uint)slot >= 7) return false;
        if (slot < 2) return (uint)id < Inventory.HandItemCount;
        if ((uint)id >= Inventory.BodyItemCount) return false;
        var kind = Inventory.KindOf(id);
        return slot switch { 2 => kind == EquipKind.Head, 3 => kind == EquipKind.Armor,
            4 => kind == EquipKind.Cape, _ => kind == EquipKind.Accessory };
    }
    private static bool Available(Request r) => Compatible(r.Slot, r.Id) &&
        (r.Id == EmptyForSlot(r.Slot) || (r.Slot < 2 ? Inventory.GetHandCount(r.Id) : Inventory.GetBodyCount(r.Id)) > 0);
    private static bool RelicAvailable(int id) => (uint)id < Inventory.RelicCount && id is not (23 or 24) &&
        (Inventory.GetRelic(id) & 1) != 0;
    private static bool CandidateAvailable(Request r) => r.Command == Command.SelectRelic ? RelicAvailable(r.Id) : Available(r);
    private static bool ValidGame()
    {
        var m = RecompOne.Runtime.Runtime.Mem;
        return m != null && Game.InGame && !Game.IsLoading && Game.StageId != Stage.Prologue &&
            Game.Character == PlayableCharacter.Alucard && m.ReadU32(Game.PlayableCharacterAddr) == 0;
    }
    private static int Step => (int)RecompOne.Runtime.Runtime.Mem!.ReadU32(MenuStepAddress);
    private static bool SelectionMatches(Request r)
    {
        if (r.Command == Command.SelectRelic)
            return Game.MenuOpen && Step == 0x22 && Inventory.RelicCursor == (r.Id >= 25 ? r.Id - 2 : r.Id);
        if (!Game.MenuOpen || Inventory.SelectedCategory != r.Slot || Step != (r.Slot < 2 ? Hand : Body)) return false;
        int index = r.Slot < 2 ? Inventory.HandCursor : Inventory.GetEquipTypeCursor(Inventory.KindOf(r.Id));
        return ItemAt(r.Slot, index) == r.Id;
    }
    private static int ItemAt(int slot, int index)
    {
        var m = RecompOne.Runtime.Runtime.Mem!;
        if (index < 0 || index >= (slot < 2 ? 169 : 90)) return -1;
        if (slot < 2) return Inventory.GetHandOrder(index);
        uint map = m.ReadU32(MappingAddress);
        if (map < 0x80000000 || map >= 0x80800000) return -1;
        uint orderIndex = m.ReadU32(map + (uint)(index * 4));
        return orderIndex < 90 ? Inventory.GetBodyOrder((int)orderIndex) : -1;
    }
    private static bool SetCandidateCursor(Request r)
    {
        var m = RecompOne.Runtime.Runtime.Mem!;
        var kind = r.Slot < 2 ? EquipKind.Hand : Inventory.KindOf(r.Id);
        int count = (int)GameApi.Call(0x800FD6C4, (uint)kind); // original list length/filter
        if (count < 1 || count > 169) return false;
        int index = -1;
        for (int i = 0; i < count; i++) if (ItemAt(r.Slot, i) == r.Id) { index = i; break; }
        if (index < 0) return false;
        uint cursor = r.Slot < 2 ? Inventory.HandCursorAddr : Inventory.EquipTypeCursorAddr + (uint)(((int)kind - 1) * 4);
        m.WriteU32(cursor, (uint)index);
        m.WriteU32(ScrollAddress + (uint)((int)kind * 4), unchecked((uint)(-12 * Math.Max(0, index / 2 - 2))));
        return true;
    }
    public static void Tick()
    {
        try { TickCore(); }
        catch (Exception ex) { Finish("Unable to prepare the selection. Use the original menu."); Console.Error.WriteLine($"[TouchEquip] {ex}"); }
    }
    private static void TickCore()
    {
        Request? request;
        lock (Gate)
        {
            if (_cancel) { _cancel = false; _active = _candidate = null; _cooldown = _verify = 0; Release(); Say("Touch selection cleared."); }
            request = _pending;
            _pending = null;
            if (request != null)
            {
                _active = request; _frames = _cooldown = _verify = 0; _exitSent = false;
                _started = Stopwatch.GetTimestamp();
                Release();
            }
        }
        if (_active == null)
        {
            if (_candidate != null && (!ValidGame() || !CandidateAvailable(_candidate) || !SelectionMatches(_candidate)))
            { _candidate = null; Say("Selection changed in the original menu."); }
            return;
        }
        if (!ValidGame()) { Finish("Available when Alucard can open his menu."); return; }
        if (Stopwatch.GetTimestamp() - _started > Stopwatch.Frequency * 12 || ++_frames > 900)
        { Finish("Menu unavailable. Wait for gameplay, then try again or press START."); return; }
        if (_cooldown > 0)
        {
            if (--_cooldown <= 6) Controller.MenuInput = 0xFFFF;
            return;
        }
        var r = _active;
        var m = RecompOne.Runtime.Runtime.Mem!;
        if (_verify > 0)
        {
            if (++_verify < 5) return;
            bool equipped = _candidate != null && (_candidate.Command == Command.SelectRelic
                ? Inventory.IsRelicActive(_candidate.Id) != _relicWasActive
                : Inventory.GetWornEquipment(_candidate.Slot) == _candidate.Id);
            Finish(equipped ? "Change applied by the game. Still paused." : "The game did not allow that change. Check the original menu.");
            return;
        }
        if (r.Command == Command.Confirm)
        {
            if (_candidate == null || !CandidateAvailable(_candidate) || !SelectionMatches(_candidate))
            { Finish("Selection changed. Select the item again."); return; }
            Say("Confirming in the original menu…", busy: true);
            if (_candidate.Command == Command.SelectRelic) _relicWasActive = Inventory.IsRelicActive(_candidate.Id);
            Pulse(Controller.Cross); _verify = 1;
            return;
        }
        _candidate = null;
        if (r.Command is Command.Back or Command.Close)
        {
            if (!Game.MenuOpen) { Finish("Game resumed."); return; }
            if (Step < Root) return; // wait for the native opening/closing animation
            if (_exitSent)
            {
                if (r.Command == Command.Back) Finish("Original menu ready. Game paused.");
                return;
            }
            Pulse(r.Command == Command.Close ? Controller.Start : Controller.Triangle);
            _exitSent = true;
            Say(r.Command == Command.Close ? "Resuming game…" : "Going back…", true);
            return;
        }
        if (!Game.MenuOpen)
        {
            if (m.ReadU32(0x8003C704) != 0 || m.ReadU32(0x8003C8B8) == 0)
            { Say("Waiting for the scene or menu transition…", true); return; }
            Say("Opening the original menu…", true);
            Pulse(Controller.Start);
            return;
        }
        int step = Step;
        if (step < Root) return; // fade/exit transitions
        if (r.Command == Command.Open) { Finish("Original menu ready. Game paused."); return; }
        bool relics = r.Command is Command.OpenRelics or Command.SelectRelic;
        int target = relics ? 0x22 : Equip;
        if (r.Command is Command.OpenEquipment or Command.OpenRelics)
        {
            if (step == target) { Finish(relics ? "Relics ready. Game paused." : "Equipment ready. Game paused."); return; }
            NavigateToOverview(step, relics);
            return;
        }
        if (r.Command == Command.SelectRelic)
        {
            if (!RelicAvailable(r.Id)) { Finish("This relic is not available in this save."); return; }
            if (step != 0x22) { NavigateToOverview(step, true); return; }
            int cursor = Inventory.RelicCursor, wanted = r.Id >= 25 ? r.Id - 2 : r.Id;
            if (cursor == wanted)
            {
                _candidate = r;
                Finish("Review the relic above, then select Toggle Relic. Game paused.", ready: true);
            }
            else
            {
                // The original US list has 28 entries, arranged in two columns.
                if ((cursor & 1) != (wanted & 1)) Pulse((wanted & 1) == 0 ? Controller.Left : Controller.Right);
                else Pulse(wanted > cursor ? Controller.Down : Controller.Up);
            }
            return;
        }
        if (!Available(r)) { Finish("Item unavailable or incompatible with this slot."); return; }
        Say("Preparing the item in the original menu…", true);
        if (step == Root)
        {
            NavigateToOverview(step, false);
        }
        else if (step == Equip)
        {
            int cursor = Inventory.SelectedCategory;
            if ((uint)cursor >= 7) { Finish("Invalid original menu cursor."); return; }
            if (cursor != r.Slot)
            {
                int down = (r.Slot - cursor + 7) % 7;
                Pulse(down <= 3 ? Controller.Down : Controller.Up);
            }
            else if (SetCandidateCursor(r)) Pulse(Controller.Cross);
            else Finish("Item not found in the original list.");
        }
        else if (step == (r.Slot < 2 ? Hand : Body) && SelectionMatches(r))
        {
            _candidate = r;
            Finish("Review the item above, then select Confirm Equipment. Game paused.", ready: true);
        }
        else if (IsStableSubmenu(step))
            Pulse(Controller.Triangle);
    }

    private static bool IsStableSubmenu(int step) => step is Equip or Hand or Body or 0x41 or 0x22 or 0x31 or 0x71 or 0x101 or 0x102 or 0x103 or 0x104 or 0x105 or 0x106 or 0x107;
    private static void NavigateToOverview(int step, bool relics)
    {
        if (step == Root)
        {
            int cursor = (int)RecompOne.Runtime.Runtime.Mem!.ReadU32(Game.MenuNavAddr);
            int target = relics ? 2 : 0;
            int down = (target - cursor + 5) % 5;
            Pulse(cursor == target ? Controller.Cross : down <= 2 ? Controller.Down : Controller.Up);
        }
        else if (IsStableSubmenu(step)) Pulse(Controller.Triangle);
    }
}
