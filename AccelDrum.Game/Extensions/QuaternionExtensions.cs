using OpenTK.Mathematics;

namespace AccelDrum.Game.Extensions;

public static class QuaternionExtensions
{
    public static Quaternion Rotate(this Quaternion q, float yawDegrees, float pitchDegrees)
    {
        // Convert degrees to radians
        float yawRadians = MathHelper.DegreesToRadians(yawDegrees);
        float pitchRadians = MathHelper.DegreesToRadians(pitchDegrees);

        // Create quaternions for yaw and pitch rotations
        Quaternion yawRotation = Quaternion.FromAxisAngle(Vector3.UnitY, yawRadians);
        Quaternion pitchRotation = Quaternion.FromAxisAngle(Vector3.UnitX, pitchRadians);

        // Combine the yaw and pitch rotations with the current orientation
        q = yawRotation * pitchRotation * q;

        // Normalize the quaternion to avoid drift
        //q.Normalize();

        return q.Normalized();
    }

}
