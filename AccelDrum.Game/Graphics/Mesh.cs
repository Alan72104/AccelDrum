using AccelDrum.Game.Graphics.Shaders;
using AccelDrum.Game.Graphics.Textures;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace AccelDrum.Game.Graphics;

public class Mesh : IDisposable
{
    private List<Vertex> vertices = new();
    public List<Vertex> Vertices
    {
        get
        {
            Dirty = true;
            return vertices;
        }
        set
        {
            Dirty = true;
            vertices = value;
        }
    }

    private List<uint> indexes = new();
    public List<uint> Indexes
    {
        get
        {
            Dirty = true;
            return indexes;
        }
        set
        {
            if (value.Max() >= vertices.Count)
                throw new InvalidOperationException($"Index out of bounds: Indexes[{value.IndexOf(value.Max())}] {value.Max()} > Vertices.Count {vertices.Count}, set the indexes after the vertices");
            Dirty = true;
            indexes = value;
        }
    }

    public bool HasIndexes => indexes.Count > 0;
    public int VertexCount => HasIndexes ? Indexes.Count : Vertices.Count;

    private Quaternion rotationQuat = Quaternion.Identity;
    public ref Quaternion RotationQuatRef
    {
        get => ref rotationQuat;
    }
    public Quaternion RotationQuat
    {
        get => rotationQuat;
        set
        {
            DirtyModel = true;
            rotationQuat = value;
            rotation = rotationQuat.ToEulerAngles() / MathF.PI * 180;
        }
    }

    private Vector3 rotation = Vector3.Zero;
    public ref Vector3 RotationRef
    {
        get => ref rotation;
    }
    /// <summary>
    /// In degrees
    /// </summary>
    public Vector3 Rotation
    {
        get => rotation;
        set
        {
            DirtyModel = true;
            rotation = new Vector3(wrapAngle(value.X), wrapAngle(value.Y), wrapAngle(value.Z));
            rotationQuat = Quaternion.FromEulerAngles(rotation / 180 * MathF.PI);

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

    private Vector3 origin = new();
    public ref Vector3 OriginRef
    {
        get => ref origin;
    }
    public Vector3 Origin
    {
        get => origin;
        set
        {
            DirtyModel = true;
            origin = value;
        }
    }

    private Vector3 position = new();
    public ref Vector3 PositionRef
    {
        get => ref position;
    }
    public Vector3 Position
    {
        get => position;
        set
        {
            DirtyModel = true;
            position = value;
        }
    }

    public string Name { get; }
    public MeshManager MeshManager { get; }
    public Shader Shader { get; set; }
    public Texture? Texture { get; set; }
    public bool Dirty { get; set; } = true;
    public bool DirtyModel { get; set; } = true;
    public PrimitiveType PrimitiveType { get; set; }
    private int vbo;
    private int vao;
    private int ebo;

    public Mesh(string name, Shader shader, MeshManager meshManager, PrimitiveType type = PrimitiveType.Triangles)
    {
        vbo = GL.GenBuffer();
        vao = GL.GenVertexArray();
        ebo = GL.GenBuffer();
        Name = name;
        Shader = shader;
        PrimitiveType = type;
        MeshManager = meshManager;
    }

    public Vertex this[int index] => HasIndexes ? vertices[(int)indexes[index]] : vertices[index];

    public void Bind()
    {
        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        Shader.Bind();
    }

    public void Draw()
    {
        MeshManager.UpdateGlobalUniforms(this);
        Bind();
        if (Dirty)
        {
            Dirty = false;
            Vertex[] verts = vertices.ToArray();
            int vertSize = Marshal.SizeOf<Vertex>();
            GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * vertSize, verts, BufferUsageHint.StreamDraw);
            uint[]? idxs = indexes.Count > 0 ? indexes.ToArray() : null;
            if (idxs is not null)
                GL.BufferData(BufferTarget.ElementArrayBuffer, idxs.Length * sizeof(uint), idxs, BufferUsageHint.StreamDraw);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, vertSize, 0);
            GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, vertSize, Marshal.OffsetOf<Vertex>(nameof(Vertex.Color)));
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, vertSize, Marshal.OffsetOf<Vertex>(nameof(Vertex.Tex)));
            GL.VertexAttribPointer(3, 1, VertexAttribPointerType.UnsignedInt, false, vertSize, Marshal.OffsetOf<Vertex>(nameof(Vertex.TexId)));
            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);
            GL.EnableVertexAttribArray(2);
            GL.EnableVertexAttribArray(3);
        }
        Texture?.Bind();

        //if (DirtyModel)
        //{
        Matrix4 model = Matrix4.Identity;
        model *= Matrix4.CreateTranslation(-origin);
        model *= Matrix4.CreateFromQuaternion(rotationQuat);
        model *= Matrix4.CreateTranslation(position);
        Shader.SetMatrix4("model", false, ref model);
        //    DirtyModel = false;
        //}

        if (indexes.Count > 0)
            GL.DrawElements(PrimitiveType, indexes.Count, DrawElementsType.UnsignedInt, 0);
        else
            GL.DrawArrays(PrimitiveType, 0, vertices.Count);
    }

    public void Clear()
    {
        Vertices.Clear();
        Indexes.Clear();
    }

    ~Mesh()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (vbo != 0)
            GL.DeleteBuffer(vbo);
        vbo = 0;
        if (vao != 0)
            GL.DeleteVertexArray(vao);
        vao = 0;
    }
}
