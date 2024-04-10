using OpenTK.Mathematics;

namespace AccelDrum.Game.Graphics;

public struct Vertex
{
    public static readonly Vertex Empty = new Vertex();
    public Vector3 Pos;
    public Vector4 Color;
    public Vector2 Tex;
    public uint TexId;
    public float X { get => Pos.X; set => Pos.X = value; }
    public float Y { get => Pos.Y; set => Pos.Y = value; }
    public float Z { get => Pos.Z; set => Pos.Z = value; }
    public float ColorX { get => Color.X; set => Color.X = value; }
    public float ColorY { get => Color.Y; set => Color.Y = value; }
    public float ColorZ { get => Color.Z; set => Color.Z = value; }
    public float ColorW { get => Color.W; set => Color.W = value; }
    public float TexX { get => Tex.X; set => Tex.X = value; }
    public float TexY { get => Tex.Y; set => Tex.Y = value; }

    public Vertex(Vector3 pos)
    {
        this.Pos = pos;
    }

    public Vertex(Vector3 pos, Vector4 color)
    {
        this.Pos = pos;
        this.Color = color;
    }

    public Vertex(Vector3 pos, Vector4 color, Vector2 tex, uint texId)
    {
        this.Pos = pos;
        this.Color = color;
        this.Tex = tex;
        this.TexId = texId;
    }

    public Vertex ToWorld(Mesh mesh)
    {
        return new Vertex()
        {
            Pos = Vector3.Transform(this.Pos - mesh.Origin, mesh.RotationQuat) + mesh.OriginRef,
            Color = this.Color,
            Tex = this.Tex,
            TexId = this.TexId
        };
    }
}
