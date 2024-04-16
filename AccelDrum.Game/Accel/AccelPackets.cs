using AccelDrum.Game.Serial;
using OpenTK.Mathematics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AccelDrum.Game.Accel;

public enum PacketType
{
    None,
    Accel,
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

    [InlineArray(64)]
    private struct Padding { private byte element0; }
};

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TextPacket
{
    public uint Length;
    public TextPacketStr Str;

    [InlineArray(SerialPacket.SizeInner - sizeof(uint))]
    public struct TextPacketStr { private byte element0; }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ConfigurePacket
{
    public enum Typ
    {
        None,
        Backlight,
        ResetDmp,
        Count
    }
    public enum Val
    {
        None = 0,
        Get = -1000,
        Result = -2000,
        Ack = -100,
        Set = 1,
        BacklightGet = Get,
        BacklightResultOn = Result,
        BacklightResultOff = Result + 1,
        BacklightAck = Ack,
        BacklightSetOn = Set,
        BacklightSetOff = Set + 1,
        BacklightSetToggle = Set + 2,
        ResetDmpAck = Ack,
    }

    public Typ Type;
    public Val Value;
    private Padding padding;

    public ConfigurePacket(Typ type, Val value)
    {
        Type = type;
        Value = value;
    }

    [InlineArray(104)]
    private struct Padding { private byte element0; }
}
