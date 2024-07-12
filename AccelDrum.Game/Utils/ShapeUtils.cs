using AccelDrum.Game.Graphics;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AccelDrum.Game.Utils;

/// <summary>
/// Shapes by ChatGPT
/// </summary>
public static class ShapeUtils
{
    private static readonly float sqrt_3 = MathF.Sqrt(3);

    public static Vertex[] EquilateralTriangle(float sideLength)
    {
        Vector3[] vecs = new[]
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(sideLength, 0f, 0f),
            new Vector3(sideLength / 2f, (sqrt_3 / 2f) * sideLength, 0f),
        };
        var transform = new Vector3(sideLength / 2f, 0, 0);
        for (int i = 0; i < vecs.Length; i++)
            vecs[i] += transform;
        return ToVertices(vecs);
    }

    public static Vertex[] Quad(Vector3 bottomLeft, Vector3 bottomRight, Vector3 topRight, Vector3 topLeft)
    {
        Vector3[] vecs =
        {
            bottomLeft, bottomRight, topRight,   // First triangle (bottom-left, bottom-right, top-right)
            topRight, topLeft, bottomLeft        // Second triangle (top-right, top-left, bottom-left)
        };
        return ToVertices(vecs);
    }

    public static List<Vertex> Cube(float w, float l, float h)
    {
        float hw = w / 2.0f;
        float hl = l / 2.0f;
        Vector3 frontBottomLeft = new(-hw, 0, hl);
        Vector3 frontBottomRight = new(hw, 0, hl);
        Vector3 frontTopRight = new(hw, h, hl);
        Vector3 frontTopLeft = new(-hw, h, hl);
        Vector3 backBottomLeft = new(-hw, 0, -hl);
        Vector3 backBottomRight = new(hw, 0, -hl);
        Vector3 backTopRight = new(hw, h, -hl);
        Vector3 backTopLeft = new(-hw, h, -hl);
        List<Vertex> verts = new();
        verts.AddRange(Quad(frontBottomLeft, frontBottomRight, frontTopRight, frontTopLeft)); // frontFace
        verts.AddRange(Quad(backBottomRight, backBottomLeft, backTopLeft, backTopRight)); // backFace
        verts.AddRange(Quad(frontTopLeft, frontTopRight, backTopRight, backTopLeft)); // topFace
        verts.AddRange(Quad(backBottomLeft, backBottomRight, frontBottomRight, frontBottomLeft)); // bottomFace
        verts.AddRange(Quad(frontBottomLeft, frontTopLeft, backTopLeft, backBottomLeft)); // leftFace
        verts.AddRange(Quad(frontTopRight, frontBottomRight, backBottomRight, backTopRight)); // rightFace
        return verts;
    }

    public static (List<Vertex>, List<uint>) CubeWithTexture(float w) => CubeWithTexture(w, w, w);

    public static (List<Vertex>, List<uint>) CubeWithTexture(float w, float l, float h)
    {
        w /= 2;
        l /= 2;
        h /= 2;
        float[] floats =
        {
            // Front
            -w, -h, l, 0, 0,
            w, -h, l, 1, 0,
            w, h, l, 1, 1,
            -w, h, l, 0, 1,

            w, -h, -l, 0, 0,
            -w, -h, -l, 1, 0,
            -w, h, -l, 1, 1,
            w, h, -l, 0, 1,

            -w, -h, -l, 0, 0,
            -w, -h, l, 1, 0,
            -w, h, l, 1, 1,
            -w, h, -l, 0, 1,

            w, -h, l, 0, 0,
            w, -h, -l, 1, 0,
            w, h, -l, 1, 1,
            w, h, l, 0, 1,

            -w, h, l, 0, 0,
            w, h, l, 1, 0,
            w, h, -l, 1, 1,
            -w, h, -l, 0, 1,

            w, -h, l, 0, 0,
            -w, -h, l, 1, 0,
            -w, -h, -l, 1, 1,
            w, -h, -l, 0, 1,
        };
        List<Vertex> vertices = VerticesFromRaw(floats);
        List<uint> indices = new()
        {
            0, 1, 2,
            0, 2, 3,
            4,5,6,
            4,6,7,
            8,9,10,
            8,10,11,
            12,13,14,
            12,14,15,
            16,17,18,
            16,18,19,
            20,21,22,
            20,22,23,
        };
        return (vertices, indices);
    }

    public static (List<Vertex>, List<uint>) Sphere(float radius, int rings, int sectors)
    {
        List<Vector3> vertices = new();
        List<int> indices = new();

        float R = 1.0f / (float)(rings - 1);
        float S = 1.0f / (float)(sectors - 1);

        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < sectors; s++)
            {
                float x = (float)(Math.Cos(2 * Math.PI * s * S) * Math.Sin(Math.PI * r * R));
                float y = (float)Math.Sin(-Math.PI / 2 + Math.PI * r * R);
                float z = (float)(Math.Sin(2 * Math.PI * s * S) * Math.Sin(Math.PI * r * R));

                vertices.Add(new Vector3(x, y, z) * radius);
            }
        }

        for (int r = 0; r < rings - 1; r++)
        {
            for (int s = 0; s < sectors - 1; s++)
            {
                indices.Add(r * sectors + s);
                indices.Add((r + 1) * sectors + s);
                indices.Add(r * sectors + (s + 1));

                indices.Add(r * sectors + (s + 1));
                indices.Add((r + 1) * sectors + s);
                indices.Add((r + 1) * sectors + (s + 1));
            }
        }

        return (ToVertices(vertices), indices.Select(i => (uint)i).ToList());
    }

    public static List<Vertex> VerticesFromRaw(float[] a)
    {
        List<Vertex> verts = new(a.Length / 5);
        for (int i = 0; i < a.Length; i += 5)
        {
            Vertex v = new()
            {
                Pos = new(a[i + 0], a[i + 1], a[i + 2]),
                Color = new(1),
                Tex = new(a[i + 3], a[i + 4]),
                TexId = 1,
            };
            verts.Add(v);
        }
        return verts;
    }

    public static Vector3[] Transform(Vector3[] vecs, in Matrix4 mat)
    {
        for (int i = 0; i < vecs.Length; i++)
            vecs[i] = Vector3.TransformPosition(vecs[i], mat);
        return vecs;
    }

    public static Vertex[] Transform(Vertex[] verts, in Matrix4 mat)
    {
        for (int i = 0; i < verts.Length; i++)
            verts[i].Pos = Vector3.TransformPosition(verts[i].Pos, mat);
        return verts;
    }

    public static List<Vertex> Transform(List<Vertex> verts, in Matrix4 mat)
    {
        for (int i = 0; i < verts.Count; i++)
        {
            Vertex vertex = verts[i];
            vertex.Pos = Vector3.TransformPosition(verts[i].Pos, mat);
            verts[i] = vertex;
        }
        return verts;
    }

    public static List<Vertex> Transform(List<Vertex> verts, in Vector3 vec)
    {
        for (int i = 0; i < verts.Count; i++)
        {
            Vertex vertex = verts[i];
            vertex.Pos += vec;
            verts[i] = vertex;
        }
        return verts;
    }

    public static List<Vertex> ToVertices(List<Vector3> vecs)
    {
        return vecs.Select(vec => new Vertex(vec)).ToList();
    }

    public static Vertex[] ToVertices(Vector3[] vecs)
    {
        Vertex[] verts = new Vertex[vecs.Length];
        for (int i = 0; i < vecs.Length; i++)
            verts[i] = new Vertex(vecs[i], new Vector4(1));
        return verts;
    }
}
