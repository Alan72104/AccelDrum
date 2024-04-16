using AccelDrum.Game.Utils;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AccelDrum.Game.Graphics.Shaders;

public class Shader : IDisposable
{
    private static Regex IncludeRegex = new Regex(@"^\s*#\s*include\s+[""<](.*)["">]");

    public struct UniformLoc : IComparable<UniformLoc>
    {
        public readonly string Name;
        public readonly int Index;

        public UniformLoc(string name, int index)
        {
            Name = name;
            Index = index;
        }

        public int CompareTo(UniformLoc other) => Name.CompareTo(other.Name);
    }

    public int Handle { get; private set; }
    public UniformLoc[] UniformLocations { get; }
    public string VertPath { get; }
    public string FragPath { get; }

    public Shader(string vertPath, string fragPath)
    {
        this.VertPath = vertPath;
        this.FragPath = fragPath;

        var shaderSource = ReadAndPreProcess(vertPath);
        var vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, shaderSource);
        CompileShader(vertexShader);

        shaderSource = ReadAndPreProcess(fragPath);
        var fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, shaderSource);
        CompileShader(fragmentShader);

        Handle = GL.CreateProgram();
        GL.AttachShader(Handle, vertexShader);
        GL.AttachShader(Handle, fragmentShader);
        LinkProgram(Handle);

        GL.DetachShader(Handle, vertexShader);
        GL.DetachShader(Handle, fragmentShader);
        GL.DeleteShader(fragmentShader);
        GL.DeleteShader(vertexShader);

        Log.Information($"Shader ({Path.GetFileName(VertPath)}, {Path.GetFileName(FragPath)}) compiled");

        MatrixPrinter printer = new()
        {
            Separator = " "
        };
        Log.Information("Uniforms:");
        GL.GetProgram(Handle, GetProgramParameterName.ActiveUniforms, out var numberOfUniforms);
        var uniformLocs = new List<UniformLoc>();
        for (var i = 0; i < numberOfUniforms; i++)
        {
            GL.GetActiveUniform(Handle, i, 100, out _, out int size, out ActiveUniformType type, out string name);
            var location = GL.GetUniformLocation(Handle, name);
            uniformLocs.Add(new(name, location));
            printer.Set(1, i, type);
            printer.Set(2, i, name);
            printer.Set(3, i, size);
        }
        uniformLocs.Sort();
        UniformLocations = uniformLocs.ToArray();
        Log.Information(printer.ToString());
        printer.Clear();

        Log.Information("Attributes:");
        GL.GetProgram(Handle, GetProgramParameterName.ActiveAttributes, out var numberOfAttributes);
        for (int i = 0; i < numberOfAttributes; i++)
        {
            GL.GetActiveAttrib(Handle, i, 100, out _, out int size, out ActiveAttribType type, out string name);
            printer.Set(1, i, type);
            printer.Set(2, i, name);
            printer.Set(3, i, size);
        }
        Log.Information(printer.ToString());
        Log.Information("");
    }

    private static string ReadAndPreProcess(string path)
    {
        StringBuilder sb = new();
        HashSet<string> included = new();
        foreach (string line in File.ReadLines(path))
        {
            var match = IncludeRegex.Match(line);
            if (match.Success)
            {
                string includeFileName = match.Groups[1].Value;
                if (!included.Contains(includeFileName))
                    sb.AppendLine(File.ReadAllText($"{Path.GetDirectoryName(path)}/{includeFileName}"));
                included.Add(includeFileName);
            }
            else
            {
                sb.AppendLine(line);
            }
        }
        return sb.ToString();
    }

    private static void CompileShader(int shader)
    {
        // Try to compile the shader
        GL.CompileShader(shader);

        // Check for compilation errors
        GL.GetShader(shader, ShaderParameter.CompileStatus, out var code);
        if (code != (int)All.True)
        {
            // We can use `GL.GetShaderInfoLog(shader)` to get information about the error.
            var infoLog = GL.GetShaderInfoLog(shader);
            throw new Exception($"Error occurred whilst compiling Shader({shader})\n\n{infoLog}");
        }
    }

    private static void LinkProgram(int program)
    {
        // We link the program
        GL.LinkProgram(program);

        // Check for linking errors
        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out var code);
        if (code != (int)All.True)
        {
            // We can use `GL.GetProgramInfoLog(program)` to get information about the error.
            var infoLog = GL.GetProgramInfoLog(program);
            throw new Exception($"Error occurred whilst linking Program({program})\n\n{infoLog}");
        }
    }

    // A wrapper function that enables the shader program.
    public void Bind()
    {
        GL.UseProgram(Handle);
    }

    // The shader sources provided with this project use hardcoded layout(location)-s. If you want to do it dynamically,
    // you can omit the layout(location=X) lines in the vertex shader, and use this in VertexAttribPointer instead of the hardcoded values.
    public int GetAttribLocation(string attribName)
    {
        return GL.GetAttribLocation(Handle, attribName);
    }

    public int GetUniformLocation(string name)
    {
        int idx = Array.BinarySearch(UniformLocations, new UniformLoc(name, 0));
        if (idx < 0)
            throw new ArgumentException($"Uniform \"{name}\" does not exist in shader \"{Path.GetFileName(VertPath)}\" \"{Path.GetFileName(FragPath)}\"");
        return UniformLocations[idx].Index;
    }

    /// <summary>
    /// Set a uniform bool (int) on this shader.
    /// </summary>
    /// <param name="name">The name of the uniform</param>
    /// <param name="data">The data to set</param>
    public void SetBool(string name, bool data)
    {
        GL.UseProgram(Handle);
        GL.Uniform1(GetUniformLocation(name), Convert.ToInt32(data));
    }

    /// <summary>
    /// Set a uniform int on this shader.
    /// </summary>
    /// <param name="name">The name of the uniform</param>
    /// <param name="data">The data to set</param>
    public void SetInt(string name, int data)
    {
        GL.UseProgram(Handle);
        GL.Uniform1(GetUniformLocation(name), data);
    }

    /// <summary>
    /// Set a uniform float on this shader.
    /// </summary>
    /// <param name="name">The name of the uniform</param>
    /// <param name="data">The data to set</param>
    public void SetFloat(string name, float data)
    {
        GL.UseProgram(Handle);
        GL.Uniform1(GetUniformLocation(name), data);
    }

    /// <summary>
    /// Set a uniform Matrix4 on this shader
    /// </summary>
    /// <param name="name">The name of the uniform</param>
    /// <param name="data">The data to set</param>
    public void SetFloat4(string name, float a, float b, float c, float d)
    {
        GL.UseProgram(Handle);
        GL.Uniform4(GetUniformLocation(name), a, b, c, d);
    }

    /// <summary>
    /// Set a uniform Matrix4 on this shader
    /// </summary>
    /// <param name="name">The name of the uniform</param>
    /// <param name="data">The data to set</param>
    public void SetMatrix4(string name, bool transpose, ref Matrix4 data)
    {
        GL.UseProgram(Handle);
        GL.UniformMatrix4(GetUniformLocation(name), transpose, ref data);
    }

    /// <summary>
    /// Set a uniform Vector3 on this shader.
    /// </summary>
    /// <param name="name">The name of the uniform</param>
    /// <param name="data">The data to set</param>
    public void SetVector3(string name, ref Vector3 data)
    {
        GL.UseProgram(Handle);
        GL.Uniform3(GetUniformLocation(name), ref data);
    }

    ~Shader()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (Handle != 0)
        {
            GL.DeleteProgram(Handle);
            Handle = 0;
        }
    }
}
