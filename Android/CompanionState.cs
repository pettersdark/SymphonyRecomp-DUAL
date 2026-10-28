using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using Sotn;

namespace RecompOne.SoTN.Android;

// Published once, then never mutated. All PS1 memory reads happen on the game thread.
internal sealed record CompanionSnapshot(bool Playing, bool Alucard, int Castle,
    byte[] Exploration, int X, int Y, string Status, string InventoryText, long Revision,
    CompanionItem[]? Items = null, int[]? Equipment = null, CompanionRelic[]? Relics = null, bool MenuOpen = false, int MenuStep = 0);
internal sealed record CompanionItem(bool Hand, int Id, string Name, int BagCount, int WornCount, EquipKind Kind);
internal sealed record CompanionRelic(int Id, string Name, bool Active);

internal static class CompanionState
{
    private static CompanionSnapshot _latest = Empty(0);
    private static long _lastCapture, _revision;
    private static bool _loggedError;
    public static CompanionSnapshot Latest => Volatile.Read(ref _latest);
    private static CompanionSnapshot Empty(long revision) => new(false, false, 0,
        new byte[CastleExploration.PackedLength], -1, -1,
        "Waiting for gameplay…", "Inventory is available when playing as Alucard.", revision);

    public static void Reset() => Volatile.Write(ref _latest, Empty(++_revision));

    public static void Capture()
    {
        long now = Stopwatch.GetTimestamp();
        if (now - _lastCapture < Stopwatch.Frequency / 5) return;
        _lastCapture = now;
        try
        {
            var m = RecompOne.Runtime.Runtime.Mem;
            long revision = ++_revision;
            if (m == null || !Game.InGame || Game.IsLoading)
            {
                Volatile.Write(ref _latest, Empty(revision));
                return;
            }
            int stage = m.ReadU8(Game.StageIdAddr);
            bool castleStage = stage != (int)Stage.Prologue;
            // During the opening Richter battle the character flag can still be zero.
            // His equipment/relic memory is not an Alucard inventory yet.
            bool alucard = castleStage && Game.Character == PlayableCharacter.Alucard;
            var packed = new byte[CastleExploration.PackedLength];
            if (castleStage)
                for (int i = 0; i < packed.Length; i++)
                    packed[i] = m.ReadU8(Map.CastleMapAddr + (uint)i);
            int x = m.ReadU8(Game.MapCursorAddr) + m.ReadU16(Game.PlayerScreenPosAddr) / 256;
            int y = m.ReadU8(Game.MapCursorAddr + 4) + m.ReadU16(Game.PlayerScreenPosAddr + 4) / 256;
            int castle = (stage & 0x20) != 0 ? 1 : 0;
            // Never display a position outside a discovered cell (transitions / prologue).
            if (stage == 0x38 || !CastleExploration.IsVisited(packed, castle, x, y)) x = y = -1;
            string status = $"{(castle == 0 ? "Normal castle" : "Inverted castle")} · Level {Player.Level} · HP {Player.Hp}/{Player.HpMax}";
            if (!castleStage) status = "Prologue · the castle map appears when the adventure begins";
            CompanionItem[] items = [];
            int[] equipment = [];
            string inventory = alucard ? ReadInventory(out items, out equipment) : "Richter · Alucard inventory is unavailable in the prologue.";
            var relics = new List<CompanionRelic>();
            if (alucard)
                for (int id = 0; id < Inventory.RelicCount; id++)
                    if (id is not (23 or 24) && (Inventory.GetRelic(id) & 1) != 0)
                        relics.Add(new(id, Name<Relic>(id), Inventory.IsRelicActive(id)));
            Volatile.Write(ref _latest, new(true, alucard, castle, packed, x, y, status, inventory, revision, items, equipment, relics.ToArray(), Game.MenuOpen,
                (int)m.ReadU32(CompanionEquipment.MenuStepAddress)));
        }
        catch (Exception ex)
        {
            Volatile.Write(ref _latest, Empty(++_revision));
            if (!_loggedError) Console.Error.WriteLine($"[Companion] Snapshot failed: {ex}");
            _loggedError = true;
        }
    }

    internal static string Name<T>(int id) where T : struct, Enum =>
        Enum.IsDefined(typeof(T), id) ? Regex.Replace(((T)Enum.ToObject(typeof(T), id)).ToString(), "(?<=[a-z0-9])(?=[A-Z])", " ") : $"Item {id}";

    private static string ReadInventory(out CompanionItem[] entries, out int[] equipped)
    {
        var lines = new List<string> { "CURRENT EQUIPMENT" };
        var items = new List<CompanionItem>();
        string[] labels = ["Right hand", "Left hand", "Head", "Armor", "Cloak", "Accessory 1", "Accessory 2"];
        equipped = new int[7];
        for (int slot = 0; slot < equipped.Length; slot++)
        {
            int id = equipped[slot] = (int)Inventory.GetWornEquipment(slot);
            lines.Add($"{labels[slot]}: {(slot < 2 ? Name<HandItem>(id) : Name<BodyItem>(id))}");
        }
        lines.Add("\nITEMS · in inventory / equipped");
        for (int id = 1; id < Inventory.HandItemCount; id++)
        {
            int n = Inventory.GetHandCount(id);
            int worn = (equipped[0] == id ? 1 : 0) + (equipped[1] == id ? 1 : 0);
            if (n + worn > 0)
            {
                lines.Add($"{Name<HandItem>(id)}   {n} / {worn}");
                items.Add(new(true, id, Name<HandItem>(id), n, worn, EquipKind.Hand));
            }
        }
        for (int id = 0; id < Inventory.BodyItemCount; id++)
        {
            if (id == Inventory.ArmorNone || id == Inventory.HeadNone || id == Inventory.CapeNone || id == Inventory.AccessoryNone) continue;
            int n = Inventory.GetBodyCount(id), worn = 0;
            for (int slot = 2; slot < 7; slot++) if (equipped[slot] == id) worn++;
            if (n + worn > 0)
            {
                lines.Add($"{Name<BodyItem>(id)}   {n} / {worn}");
                items.Add(new(false, id, Name<BodyItem>(id), n, worn, Inventory.KindOf(id)));
            }
        }
        lines.Add("\nRELICS");
        for (int id = 0; id < Inventory.RelicCount; id++)
            if (Inventory.HasRelic(id)) lines.Add($"{Name<Relic>(id)}{(Inventory.IsRelicActive(id) ? " · ON" : "")}");
        lines.Add("\nLEARNED SPELLS");
        for (int id = 0; id < Inventory.SpellCount; id++)
            if (Inventory.HasSpell((Spell)id)) lines.Add(Name<Spell>(id));
        entries = items.ToArray();
        lines.Add("\nSelect a slot or item to prepare equipment in the original menu.");
        return string.Join("\n", lines);
    }
}
