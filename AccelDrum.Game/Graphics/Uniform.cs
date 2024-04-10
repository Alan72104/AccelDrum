using AccelDrum.Game.Graphics.Shaders;
using OpenTK.Mathematics;
using System;

namespace AccelDrum.Game.Graphics;

public class Uniform<T> : IUniform where T : unmanaged
{
    public string Name { get; }
    public T Value;

    public Uniform(string name)
    {
        Name = name;
    }

    public void Update(Shader shader)
    {
        switch (this)
        {
            case Uniform<Matrix4> matrix4:
                shader.SetMatrix4(Name, false, ref matrix4.Value);
                break;
            case Uniform<Vector3> vector3:
                shader.SetVector3(Name, ref vector3.Value);
                break;
            case Uniform<float> @float:
                shader.SetFloat(Name, @float.Value);
                break;
            default:
                throw new NotImplementedException($"Uniform<T> of type {this.GetType()} not implemented");
        }
    }

    public object Get()
    {
        return Value;
    }
}
