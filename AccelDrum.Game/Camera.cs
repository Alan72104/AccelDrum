using AccelDrum.Game.Extensions;
using OpenTK.Mathematics;
using System;
//using System.Numerics;

namespace AccelDrum;

// This is the camera class as it could be set up after the tutorials on the website.
// It is important to note there are a few ways you could have set up this camera.
// For example, you could have also managed the player input inside the camera class,
// and a lot of the properties could have been made into functions.

// TL;DR: This is just one of many ways in which we could have set up the camera.
// Check out the web version if you don't know why we are doing a specific thing or want to know more about the code.
public class Camera
{
    // Those vectors are directions pointing outwards from the camera to define how it rotated.
    private Vector3 _front = -Vector3.UnitZ;

    private Vector3 _up = Vector3.UnitY;

    private Vector3 _right = Vector3.UnitX;

    public const float Epsilon = 0.001f;
    /// <summary>
    /// In degrees
    /// </summary>
    public static readonly Vector3 DefaultRot = new Vector3(0, -90, 0);
    public static readonly Quaternion DefaultRotQuat = Quaternion.FromEulerAngles(DefaultRot / 180 * MathF.PI);

    // Rotation around the X axis (radians)
    //private float _pitch;

    // Rotation around the Y axis (radians)
    //private float _yaw = -MathHelper.PiOver2; // Without this, you would be started rotated 90 degrees right.

    // The field of view of the camera (radians)
    private float _fov = MathHelper.PiOver2;

    private Vector3 _rotation = DefaultRot;

    private Quaternion _rotationQuat = DefaultRotQuat;

    public Quaternion RotationQuat
    {
        get => _rotationQuat;
        set
        {
            _rotationQuat = value;
            _rotationQuat.Normalize();
            _rotation = _rotationQuat.ToEulerAngles() / MathF.PI * 180;
            UpdateVectors();
        }
    }

    public Vector3 Rotation
    {
        get => _rotation;
        set
        {
            _rotation = new Vector3(
                MathHelper.Clamp(value.X, -90 + Epsilon, 90 - Epsilon),
                wrapAngle(value.Y),
                wrapAngle(value.Z));
            _rotationQuat = Quaternion.FromEulerAngles(_rotation / 180 * MathF.PI);
            _rotationQuat.Normalize();
            UpdateVectors();

            static float wrapAngle(float angle)
            {
                while (angle > 180.0f)
                    angle -= 360.0f;
                while (angle < -180.0f)
                    angle += 360.0f;
                return angle;
            }
        }
    }

    public Camera(Vector3 position, float aspectRatio)
    {
        Position = position;
        AspectRatio = aspectRatio;
    }

    public Vector3 Position { get; set; }

    public float AspectRatio { private get; set; }

    public Vector3 Front => _front;

    public Vector3 Up => _up;

    public Vector3 Right => _right;

    public float Pitch
    {
        get => _rotation.X;
        set
        {
            _rotation.X = value;
            Rotation = _rotation;
            UpdateVectors();
        }
    }

    public float Yaw
    {
        get => _rotation.Y;
        set
        {
            _rotation.Y = value;
            Rotation = _rotation;
            UpdateVectors();
        }
    }

    public float Roll
    {
        get => _rotation.Z;
        set
        {
            _rotation.Z = value;
            Rotation = _rotation;
            UpdateVectors();
        }
    }

    // The field of view (FOV) is the vertical angle of the camera view.
    // This has been discussed more in depth in a previous tutorial,
    // but in this tutorial, you have also learned how we can use this to simulate a zoom feature.
    // We convert from degrees to radians as soon as the property is set to improve performance.
    public float Fov
    {
        get => MathHelper.RadiansToDegrees(_fov);
        set
        {
            var angle = MathHelper.Clamp(value, 1f, 90f);
            _fov = MathHelper.DegreesToRadians(angle);
        }
    }

    // Get the view matrix using the amazing LookAt function described more in depth on the web tutorials
    public Matrix4 GetViewMatrix()
    {
        return Matrix4.LookAt(Position, Position + _front, _up) * Matrix4.CreateFromAxisAngle(Vector3.UnitZ, _rotation.Z / 180 * MathF.PI);
    }

    // Get the projection matrix using the same method we have used up until this point
    public Matrix4 GetProjectionMatrix()
    {
        return Matrix4.CreatePerspectiveFieldOfView(_fov, AspectRatio, 0.01f, 100f);
    }

    // This function is going to update the direction vertices using some of the math learned in the web tutorials.
    private void UpdateVectors()
    {
        // First, the front matrix is calculated using some basic trigonometry.
        _front.X = MathF.Cos(Pitch.ToRadians()) * MathF.Cos(Yaw.ToRadians());
        _front.Y = MathF.Sin(Pitch.ToRadians());
        _front.Z = MathF.Cos(Pitch.ToRadians()) * MathF.Sin(Yaw.ToRadians());
        //_front = Vector3.Transform(-Vector3.UnitZ, _rotationQuat);

        // We need to make sure the vectors are all normalized, as otherwise we would get some funky results.
        _front = Vector3.Normalize(_front);

        // Calculate both the right and the up vector using cross product.
        // Note that we are calculating the right from the global up; this behaviour might
        // not be what you need for all cameras so keep this in mind if you do not want a FPS camera.
        _right = Vector3.Normalize(Vector3.Cross(_front, Vector3.UnitY));
        _up = Vector3.Normalize(Vector3.Cross(_right, _front));
    }
}