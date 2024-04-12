using System;
using System.IO.Hashing;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AccelDrum.Game.Serial;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SerialPacket
{
    public static readonly int Size = Unsafe.SizeOf<SerialPacket>();
    public const int SizeExpected = 64;
    public const int SizeInner = 48;
    public const ulong MagicExpected = 0xDEADBEEF80085069;

    public uint Type;
    public InnerData Inner;
    public uint Crc32;
    public ulong Magic;

    public T GetInnerAs<T>() where T : struct
    {
        CheckInnerSize<T>();
        ref SerialPacket<T> typed = ref Unsafe.As<SerialPacket, SerialPacket<T>>(ref this);
        return typed.Inner;
    }

    public uint GetCrc32()
    {
        return System.IO.Hashing.Crc32.HashToUInt32(
            MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.As<uint, byte>(ref Type),
                sizeof(uint) + SerialPacket.SizeInner)
        );
    }

    public static ref SerialPacket RefFromTyped<T>(ref SerialPacket<T> typed) where T : struct
    {
        return ref Unsafe.As<SerialPacket<T>, SerialPacket>(ref typed);
    }

    public static void CheckInnerSize<T>() where T : struct
    {
        int size = Unsafe.SizeOf<T>();
        if (size != SerialPacket.SizeInner)
            throw new InvalidOperationException($"Inner packet size should be {SerialPacket.SizeInner} but is {size}");
    }
}

[InlineArray(SerialPacket.SizeInner)]
public struct InnerData
{
    private byte element0;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SerialPacket<T> where T : struct
{
    public uint Type;
    public T Inner;
    public uint Crc32;
    public ulong Magic;

    public uint GetCrc32()
    {
        return System.IO.Hashing.Crc32.HashToUInt32(
            MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.As<uint, byte>(ref Type),
                sizeof(uint) + SerialPacket.SizeInner)
        );
    }

    public static ref SerialPacket<T> RefFromUntyped(ref SerialPacket untyped)
    {
        SerialPacket.CheckInnerSize<T>();
        return ref Unsafe.As<SerialPacket, SerialPacket<T>>(ref untyped);
    }
}
