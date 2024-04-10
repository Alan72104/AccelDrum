using System.Runtime.InteropServices;

namespace AccelDrum.Game.Serial;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct SerialPacket
{
    public static readonly int Size = Marshal.SizeOf<SerialPacket>();
    public const int SizeExpected = 64;
    public const int SizeInner = 52;
    public const ulong MagicExpected = 0xDEADBEEF80085069;

    public fixed byte Inner[SizeInner];
    public uint Crc32;
    public ulong Magic;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SerialPacket<T> where T : struct
{
    public T Inner;
    public uint Crc32;
    public ulong Magic;
}
