using OpenTK.Mathematics;

namespace AccelDrum.Game.Accel;

public class AccelSettings
{
    public AccelFullScale AccelRange { get; set; }
    public GyroFullScale GyroRange { get; set; }
    public required Vector3i[] AccelFactoryTrims { get; set; }
    public required Vector3i[] GyroFactoryTrims { get; set; }
}

public enum AccelFullScale
{
    A2G,
    A4G,
    A8G,
    A16G
}

public enum GyroFullScale
{
    G250DPS,
    G500DPS,
    G1000DPS,
    G2000DPS
}
