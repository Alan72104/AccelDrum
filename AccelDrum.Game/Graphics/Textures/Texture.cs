using OpenTK.Graphics.OpenGL4;
using Serilog;
using StbImageSharp;
using System;
using System.IO;

namespace AccelDrum.Game.Graphics.Textures;

public class Texture : IDisposable
{
    public string Path { get; }
    public int Handle { get; private set; }
    public TextureTarget Target { get; }

    public Texture(string path, TextureTarget target = TextureTarget.Texture2D)
    {
        Path = path;
        Target = target;
        Handle = GL.GenTexture();
        StbImage.stbi_set_flip_vertically_on_load(1);
        ImageResult image = ImageResult.FromStream(File.OpenRead(path), ColorComponents.RedGreenBlueAlpha);
        Log.Information($"Loaded image {System.IO.Path.GetFileNameWithoutExtension(path)} size: {image.Width}x{image.Height}");
        Bind();
        SetParameters();
        GL.TexImage2D(Target, 0, PixelInternalFormat.Rgba, image.Width, image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, image.Data);
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
    }

    public void Bind(uint index = 0)
    {
        GL.BindTexture((TextureTarget)((int)Target + index), Handle);
    }

    public void SetParameters(TextureMinFilter minFilter = TextureMinFilter.Linear, TextureMagFilter magFilter = TextureMagFilter.Linear)
    {
        Bind();
        GL.TexParameter(Target, TextureParameterName.TextureMinFilter, (int)minFilter);
        GL.TexParameter(Target, TextureParameterName.TextureMagFilter, (int)magFilter);
        GL.TexParameter(Target, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(Target, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
    }

    ~Texture()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (Handle != 0)
        {
            GL.DeleteTexture(Handle);
            Handle = 0;
        }
    }
}
