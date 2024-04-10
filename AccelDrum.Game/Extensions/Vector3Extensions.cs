using AccelDrum.Game.Graphics;
using OpenTK.Mathematics;
using System;
using System.Runtime.CompilerServices;
using Vector3Net = System.Numerics.Vector3;

namespace AccelDrum.Game.Extensions;

public static class Vector3Extensions
{
    public static ref Vector3Net Interchange(ref this Vector3 v)
    {
        return ref Unsafe.As<Vector3, Vector3Net>(ref v);
    }

    public static ref Vector3 Interchange(ref this Vector3Net v)
    {
        return ref Unsafe.As<Vector3Net, Vector3>(ref v);
    }

    public static Vector3 ToWorld(this Vector3 v, Mesh mesh)
    {
        return Vector3.Transform(v - mesh.OriginRef, mesh.RotationQuatRef) + mesh.PositionRef;
    }
}
