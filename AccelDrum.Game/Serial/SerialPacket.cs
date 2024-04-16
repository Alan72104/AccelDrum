using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AccelDrum.Game.Serial;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SerialPacket
{
    public static readonly int Size = Unsafe.SizeOf<SerialPacket>();
    public const int SizeExpected = 128;
    public const int SizeInner = SizeExpected - sizeof(uint) - sizeof(uint) - sizeof(ulong);
    public const ulong MagicExpected = 0xDEADBEEF80085069;
    public static readonly ulong MagicExpectedReversed = BinaryPrimitives.ReverseEndianness(MagicExpected);

    public uint Type;
    public InnerData Inner;
    public uint Crc32;
    public ulong Magic;

    /// <summary>
    /// Gets a copy of the inner data as type <typeparamref name="T"/>
    /// </summary>
    public T GetInnerAs<T>() where T : struct
    {
        CheckInnerSize<T>();
        ref SerialPacket<T> typed = ref Unsafe.As<SerialPacket, SerialPacket<T>>(ref this);
        return typed.Inner;
    }

    /// <summary>
    /// Gets the crc32 of <see cref="Type"/> + <see cref="Inner"/>
    /// </summary>
    public uint GetCrc32()
    {
        return System.IO.Hashing.Crc32.HashToUInt32(
            MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.As<uint, byte>(ref Type),
                sizeof(uint) + SerialPacket.SizeInner)
        );
    }

    /// <summary>
    /// Reinterprets a typed packet ref as untyped, doesn't check the size
    /// </summary>
    public static ref SerialPacket RefFromTyped<T>(ref SerialPacket<T> typed) where T : struct
    {
        return ref Unsafe.As<SerialPacket<T>, SerialPacket>(ref typed);
    }

    /// <summary>
    /// Asserts that the managed size of <typeparamref name="T"/> is equal to <see cref="SizeInner"/>
    /// </summary>
    /// <exception cref="InvalidOperationException">When sizes don't match</exception>
    public static void CheckInnerSize<T>() where T : struct
    {
        int size = Unsafe.SizeOf<T>();
        if (size != SerialPacket.SizeInner)
            throw new InvalidOperationException($"Inner packet size should be {SerialPacket.SizeInner} but is {size}, type: {typeof(T)}");
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

    /// <inheritdoc cref="SerialPacket.GetCrc32"/>
    public uint GetCrc32()
    {
        return System.IO.Hashing.Crc32.HashToUInt32(
            MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.As<uint, byte>(ref Type),
                sizeof(uint) + SerialPacket.SizeInner)
        );
    }

    /// <summary>
    /// Reinterprets an untyped packet ref as type <typeparamref name="T"/>, doesn't check the size
    /// </summary>
    public static ref SerialPacket<T> RefFromUntyped(ref SerialPacket untyped)
    {
        SerialPacket.CheckInnerSize<T>();
        return ref Unsafe.As<SerialPacket, SerialPacket<T>>(ref untyped);
    }
}
