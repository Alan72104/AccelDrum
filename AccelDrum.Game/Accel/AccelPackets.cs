using AccelDrum.Game.Serial;
using OpenTK.Mathematics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AccelDrum.Game.Accel;

public enum AccelPacketType
{
    None,
    Accel,
    Text,
    Count
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct AccelPacket
(
    ulong DeltaMicros,
    Vector3 Accel,
    Quaternion Gyro,
    Vector3 GyroEuler
);

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TextPacket
{
    public uint Length;
    public TextPacketStr Str;
}

[InlineArray(SerialPacket.SizeInner - sizeof(uint))]
public struct TextPacketStr
{
    private byte element0;
}
