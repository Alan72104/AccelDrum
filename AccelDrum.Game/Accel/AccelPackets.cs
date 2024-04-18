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
        Backlight,
        ResetDmp,
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
        ResetDmpAck
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
