using AccelDrum.Game.Graphics.Shaders;
using AccelDrum.Game.Graphics.Textures;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AccelDrum.Game.Graphics;

public class MeshManager : IDisposable
{
    public Dictionary<string, IUniform> GlobalUniforms { get; } = new();
    public Dictionary<string, Mesh> Meshes { get; } = new();
    public Dictionary<string, Shader> Shaders { get; } = new();
    public Dictionary<string, Texture> Textures { get; } = new();

    public Mesh this[string name]
    {
        get => Meshes[name];
    }

    public Mesh CreateMesh(string name, Shader shader, PrimitiveType type = PrimitiveType.Triangles)
    {
        Meshes[name] = new Mesh(name, shader, this, type);
        return Meshes[name];
    }

    public Shader CreateShader(string name, string vert, string frag)
    {
        Shaders[name] = new Shader(vert, frag);
        return Shaders[name];
    }


    public Texture CreateTexture(string name, string path)
    {
        Textures[name] = new Texture(path);
        return Textures[name];
    }

    public Uniform<T> CreateGlobalUniform<T>(string name) where T : unmanaged
    {
        var uniform = new Uniform<T>(name);
        GlobalUniforms[name] = uniform;
        return uniform;
    }

    public void UpdateGlobalUniforms(Mesh mesh)
    {
        foreach ((string uniformName, IUniform uniform) in GlobalUniforms)
        {
            if (mesh.Shader.UniformLocations.Any(uniformLoc => uniformLoc.Name == uniformName))
            {
                uniform.Update(mesh.Shader);
            }
        }
    }

    ~MeshManager()
    {
        Dispose();
    }

    public void Dispose()
    {
        foreach (Mesh mesh in Meshes.Values)
            mesh.Dispose();
        Meshes.Clear();
        foreach (Shader shader in Shaders.Values)
            shader.Dispose();
        Shaders.Clear();
        foreach (Texture texture in Textures.Values)
            texture.Dispose();
        Textures.Clear();
    }
}
