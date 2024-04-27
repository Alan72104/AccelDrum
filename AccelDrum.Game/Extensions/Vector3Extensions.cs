using AccelDrum.Game.Graphics;
using OpenTK.Mathematics;
using System.Runtime.CompilerServices;
using Vector3Net = System.Numerics.Vector3;

namespace AccelDrum.Game.Extensions;

public static class Vector3Extensions
{
    public static ref Vector3Net InterchangeRef(ref this Vector3 v)
    {
        return ref Unsafe.As<Vector3, Vector3Net>(ref v);
    }

    public static ref Vector3 InterchangeRef(ref this Vector3Net v)
    {
        return ref Unsafe.As<Vector3Net, Vector3>(ref v);
    }

    public static Vector3Net Interchange(this Vector3 v)
    {
        return new Vector3Net(v.X, v.Y, v.Z);
    }

    public static Vector3 Interchange(this Vector3Net v)
    {
        return new Vector3(v.X, v.Y, v.Z);
    }

    public static Vector3 ToWorld(this Vector3 v, Mesh mesh)
    {
        return Vector3.Transform(v - mesh.OriginRef, mesh.RotationQuatRef) + mesh.PositionRef;
    }
}
