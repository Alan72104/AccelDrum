using System;
using System.Numerics;

namespace AccelDrum.Game.Extensions;

public static class NumberExtensions
{
    public static float ToRadians(this float v)
    {
        return v / 180 * MathF.PI;
    }

    public static float ToDegrees(this float v)
    {
        return v / MathF.PI * 180;
    }

    public static T RealOr0<T>(this T v) where T : INumberBase<T>
    {
        return T.IsRealNumber(v) ? v : T.Zero;
    }

    public static T RealAndNotZeroOr1<T>(this T v) where T : INumberBase<T>
    {
        return T.IsRealNumber(v) && !T.IsZero(v) ? v : T.One;
    }
}
