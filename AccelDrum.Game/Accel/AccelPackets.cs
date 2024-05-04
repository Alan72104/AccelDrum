using AccelDrum.Game.Serial;
using OpenTK.Mathematics;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AccelDrum.Game.Accel;

public enum PacketType
{
    None,
    Accel,
    RawAccel,
    Text,
    Configure,
    Count
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct AccelPacket
{
    public ulong DeltaMicros;
    public Vector3 Accel;
    public Quaternion Gyro;
    public Vector3 GyroEuler;
    private Padding padding;

    [InlineArray(SerialPacket.SizeInner - sizeof(ulong) - sizeof(float) * 10)]
    private struct Padding { private byte element0; }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RawAccelPacket
{
    public struct Pack
    {
        public uint DeltaMicros;
        public Vector3 Accel;
        public Vector3 Gyro;
    }
    public PackArray Packs;
    private Padding padding;

    [InlineArray(4)]
    public struct PackArray { private Pack element0; }

    [InlineArray(16)]
    private struct Padding { private byte element0; }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TextPacket
{
    public uint Length;
    public bool HasNext;
    public TextPacketStr Str;

    [InlineArray(SerialPacket.SizeInner - sizeof(uint) - sizeof(bool))]
    public struct TextPacketStr { private byte element0; }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ConfigurePacket
{
    public enum Typ
    {
        None,
        PollForData,
        Backlight,
        Reset,
        Count
    }
    public enum Val
    {
        None = 0,
        BacklightGet,
        BacklightResultOn,
        BacklightResultOff,
        BacklightAck,
        BacklightSetOn,
        BacklightSetOff,
        BacklightSetToggle,
        ResetAck,
        ResetResultSettings,
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Settings
    {
        public byte AccelRange;
        public byte GyroRange;
        public ByteArray AccelFactoryTrims;
        public ByteArray GyroFactoryTrims;

        [InlineArray(12)]
        public struct ByteArray { private byte element0; }
    };

    public Typ Type;
    public Val Value;
    public ExtraData Data;

    public ConfigurePacket(Typ type, Val value)
    {
        Type = type;
        Value = value;
    }

    public unsafe ref T GetDataAs<T>() where T : struct
    {
        if (Unsafe.SizeOf<T>() > Unsafe.SizeOf<ExtraData>())
            throw new InvalidOperationException($"Size to cast to should be equal to or less than ConfigurePacket.ExtraData");
        fixed (ExtraData* ptr = &Data)
            return ref Unsafe.AsRef<T>(ptr); // Super unsafe workaround for referencing self (ref Data)
    }

    [InlineArray(SerialPacket.SizeInner - sizeof(Typ) - sizeof(Val))]
    public struct ExtraData { private byte element0; }
}
