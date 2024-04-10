using OpenTK.Mathematics;
using System;

namespace AccelDrum.Game.Utils;

public class VectorUtils
{
    public static (float yaw, float pitch) ToYawPitch(Vector3 from, Vector3 to)
    {
        return ToYawPitch(to - from);
    }

    public static (float yaw, float pitch) ToYawPitch(Vector3 lookAt)
    {
        // Calculate Pitch
        float pitch = (float)Math.Atan2(lookAt.Y, Math.Sqrt(lookAt.X * lookAt.X + lookAt.Z * lookAt.Z));

        // Calculate Yaw
        Vector2 lookAtNoY = new Vector2(lookAt.X, lookAt.Z); // Project onto horizontal plane
        lookAtNoY.Normalize();
        Vector2 referenceVector = new Vector2(0, 1); // Forward direction
        referenceVector.Normalize();
        float cosAngle = Vector2.Dot(lookAtNoY, referenceVector);
        float yaw = (float)Math.Acos(cosAngle);

        return (MathHelper.RadiansToDegrees(yaw), MathHelper.RadiansToDegrees(pitch));
    }

    public static Vector2 ToYawPitch(Quaternion quaternion)
    {
        // Calculate pitch (rotation around x-axis)
        float pitch = (float)Math.Asin(2 * (quaternion.Y * quaternion.Z - quaternion.W * quaternion.X));

        // Calculate yaw (rotation around y-axis)
        float yaw = (float)Math.Atan2(2 * (quaternion.X * quaternion.Y + quaternion.W * quaternion.Z),
                                       1 - 2 * (quaternion.Y * quaternion.Y + quaternion.Z * quaternion.Z));

        return new Vector2(yaw, pitch);
    }

    public static Quaternion FromYawPitch(Vector2 yawPitch)
    {
        float halfYaw = yawPitch.X * 0.5f;
        float halfPitch = yawPitch.Y * 0.5f;

        float cosYaw = (float)Math.Cos(halfYaw);
        float sinYaw = (float)Math.Sin(halfYaw);
        float cosPitch = (float)Math.Cos(halfPitch);
        float sinPitch = (float)Math.Sin(halfPitch);

        float x = cosYaw * sinPitch;
        float y = sinYaw * sinPitch;
        float z = cosPitch * sinYaw;
        float w = cosYaw * cosPitch;

        return new Quaternion(x, y, z, w);
    }
}
