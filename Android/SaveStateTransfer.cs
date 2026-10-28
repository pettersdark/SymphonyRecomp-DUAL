using System;
using System.IO;
using System.Text;

namespace RecompOne.SoTN.Android;

// File transfer only: never touches live CPU/RAM or requests a state load.
internal static class SaveStateTransfer
{
    private const int MaxBytes = 32 * 1024 * 1024;
    private static string SlotPath(string baseDir, int slot)
    {
        if (slot is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(slot));
        return Path.Combine(baseDir, "savestates", $"slot_{slot}.sav");
    }

    public static byte[] ReadValidated(Stream source)
    {
        using var output = new MemoryStream();
        var buffer = new byte[65536];
        int count;
        while ((count = source.Read(buffer, 0, buffer.Length)) != 0)
        {
            if (output.Length + count > MaxBytes) throw new InvalidDataException("State file exceeds 32 MB.");
            output.Write(buffer, 0, count);
        }
        var bytes = output.ToArray();
        Validate(bytes);
        return bytes;
    }

    public static string? Import(string baseDir, int slot, Stream source)
    {
        string target = SlotPath(baseDir, slot);
        byte[] bytes = ReadValidated(source); // reject invalid input before changing files
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        string? backup = null;
        try
        {
            File.WriteAllBytes(temporary, bytes);
            if (File.Exists(target))
            {
                backup = target + ".backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N");
                File.Copy(target, backup);
            }
            File.Move(temporary, target, overwrite: true);
            return backup;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static void Export(string baseDir, int slot, Stream destination)
    {
        using var source = File.OpenRead(SlotPath(baseDir, slot));
        var bytes = ReadValidated(source);
        destination.Write(bytes, 0, bytes.Length);
    }

    // Exact layout of current SOTNSS05, including all variable-length sections.
    // Reject old memory cards, interrupted saves and truncated/corrupt payloads.
    public static void Validate(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        void Skip(long count)
        {
            if (count < 0 || count > stream.Length - stream.Position)
                throw new InvalidDataException("State file is incomplete.");
            stream.Position += count;
        }
        void Sized(int expected)
        {
            if (reader.ReadInt32() != expected) throw new InvalidDataException("Unsupported state layout.");
            Skip(expected);
        }
        try
        {
            if (bytes.Length > MaxBytes || Encoding.ASCII.GetString(reader.ReadBytes(8)) != "SOTNSS05")
                throw new InvalidDataException("Select a current SymphonyRecomp state (.sav), not a PlayStation memory card.");
            Skip(8 + 31 * 4); // timestamp and GPR0..30
            if (reader.ReadUInt32() != 0x800E3DDCu)
                throw new InvalidDataException("This state was saved during a transition and cannot be loaded safely.");
            Skip(5 * 4); // HI, LO, SR, CAUSE, EPC
            int overlays = reader.ReadInt32();
            if (overlays is < 1 or > 64) throw new InvalidDataException("Invalid overlay list.");
            for (int i = 0; i < overlays; i++)
            {
                int length = reader.Read7BitEncodedInt();
                if (length is < 1 or > 128) throw new InvalidDataException("Invalid overlay name.");
                Skip(length);
            }
            int ram = reader.ReadInt32();
            if (ram is not (0x200000 or 0x800000)) throw new InvalidDataException("Unsupported RAM size.");
            Skip(ram);
            Sized(1024); // scratchpad
            Sized(8192); // hardware registers
            Skip(98); // GPU scalar state
            if (reader.ReadInt32() != 1024 * 512) throw new InvalidDataException("Invalid video memory size.");
            Skip(1024 * 512 * 2);
            if (reader.ReadBoolean())
            {
                if (reader.ReadInt32() != 1) throw new InvalidDataException("Unsupported sound state.");
                Sized(512 * 1024);
                if (reader.ReadInt32() != 24) throw new InvalidDataException("Invalid voice count.");
                for (int i = 0; i < 24; i++)
                {
                    Skip(55);
                    if (reader.ReadInt32() != 31) throw new InvalidDataException("Invalid voice buffer.");
                    Skip(31 * 2);
                }
                Skip(92); // SPU mixer and global registers
            }
            if (reader.ReadInt32() != 1) throw new InvalidDataException("Unsupported CD state.");
            Sized(4); Sized(8);
            Skip(3 + 4 + 12 + 2 + 2); // CD scalars, callbacks, flags and filter
            if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected data at the end of the state.");
        }
        catch (EndOfStreamException ex) { throw new InvalidDataException("State file is incomplete.", ex); }
        catch (FormatException ex) { throw new InvalidDataException("Invalid state structure.", ex); }
    }
}
