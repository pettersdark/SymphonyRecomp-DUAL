using System;

namespace RecompOne.SoTN.Android;

// US original, src/dra/5087C.c func_800F2014/func_800F2120:
// four cells per byte, two bits each, high pair first; 0x400 bytes per castle.
// The low bit means visited. The high bit is the librarian's purchased blueprint.
internal static class CastleExploration
{
    public const int PackedLength = 0x800;
    public static bool IsVisited(ReadOnlySpan<byte> packed, int castle, int x, int y)
    {
        if (packed.Length != PackedLength || castle is < 0 or > 1 ||
            (uint)x >= 64 || (uint)y >= 64) return false;
        int index = castle * 0x400 + y * 16 + (x >> 2);
        return (packed[index] & (1 << ((3 - (x & 3)) * 2))) != 0;
    }
}
