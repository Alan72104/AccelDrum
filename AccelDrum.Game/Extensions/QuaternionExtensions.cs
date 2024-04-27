using OpenTK.Mathematics;
using QuaternionNet = System.Numerics.Quaternion;

namespace AccelDrum.Game.Extensions;

public static class QuaternionExtensions
{
    public static QuaternionNet Interchange(this Quaternion v)
    {
        return new QuaternionNet(v.X, v.Y, v.Z, v.W);
    }

    public static Quaternion Interchange(this QuaternionNet v)
    {
        return new Quaternion(v.X, v.Y, v.Z, v.W);
    }
}
