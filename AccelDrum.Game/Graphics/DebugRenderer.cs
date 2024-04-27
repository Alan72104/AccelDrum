using AccelDrum.Game.Extensions;
using AccelDrum.Game.Utils;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using static ImGuiNET.ImGui;
using static OpenTK.Graphics.OpenGL.GL;
using Vector2Net = System.Numerics.Vector2;

namespace AccelDrum.Game.Graphics;

public class DebugRenderer : IDisposable
{
    public static DebugRenderer Ins { get; private set; } = null!;
    private static readonly IReadOnlyList<Vertex> DebugBall;
    private static readonly IReadOnlyList<uint> DebugBallIndexes;
    private MeshManager meshManager;
    private Mesh meshMe;
    private List<Mesh> meshes = new();
    private Mesh? SelectedMesh => selectedMeshIndex > -1 ? meshes[selectedMeshIndex] : null;
    private string[] meshNames = [""];
    private int selectedMeshIndex = -1;
    private int selectedVertexIndex = -1;

    static DebugRenderer()
    {
        (DebugBall, DebugBallIndexes) = ShapeUtils.Sphere(0.125f / 2, 10, 10);
    }

    public static void Init(MeshManager meshManager, Mesh mesh)
    {
        Ins = new DebugRenderer(meshManager, mesh);
    }

    public DebugRenderer(MeshManager meshManager, Mesh mesh)
    {
        this.meshManager = meshManager;
        this.meshMe = mesh;
    }

    public void AddMeshes(params Mesh[] meshes)
    {
        this.meshes.AddRange(meshes);
        meshNames = ["", .. meshes.Select(m => m.Name)];
    }

    public void AddAllMeshes()
    {
        var meshes = meshManager.Meshes.Values.ToArray();
        this.meshes.AddRange(meshes);
        meshNames = ["", .. meshes.Select(m => m.Name)];
    }

    public void SetMesh(Mesh mesh)
    {
        if (meshes.Contains(mesh))
        {
            selectedMeshIndex = meshes.IndexOf(mesh);
            selectedVertexIndex = -1;
        }
    }

    public void DebugWindow()
    {
        Begin("Debug");

        if (CollapsingHeader("Global uniforms"))
        {
            if (BeginTable("tableUniforms", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
            {
                TableSetupColumn("Type");
                TableSetupColumn("Name");
                TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
                TableHeadersRow();

                int tableId = 0;
                foreach (var (name, uniform) in meshManager.GlobalUniforms)
                {
                    Type type = uniform.GetType().GenericTypeArguments[0];
                    TableNextRow();
                    TableSetColumnIndex(0);
                    Text(type.Name);
                    TableSetColumnIndex(1);
                    Text(name);
                    TableSetColumnIndex(2);
                    switch (uniform.Get())
                    {
                        case Matrix4 m4:
                            printMatrix(ref m4, 4, 4);
                            break;
                        case Matrix3 m3:
                            printMatrix(ref m3, 3, 3);
                            break;
                        case Matrix2 m2:
                            printMatrix(ref m2, 2, 2);
                            break;
                        case Vector4 v4:
                            printMatrix(ref v4, 4, 1);
                            break;
                        case Vector3 v3:
                            printMatrix(ref v3, 3, 1);
                            break;
                        case Vector2 v2:
                            printMatrix(ref v2, 2, 1);
                            break;
                        default:
                            Text(uniform.Get().ToString());
                            break;
                    }

                    void printMatrix<T>(ref T obj, int cols, int rows)
                    {
                        if (BeginTable("tableUniformValue" + tableId++, 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                        {
                            SetWindowFontScale(0.75f);
                            for (int i = 0; i < rows; i++)
                            {
                                TableNextRow(ImGuiTableRowFlags.None, ImGui.GetTextLineHeightWithSpacing());
                                for (int j = 0; j < cols; j++)
                                {
                                    TableNextColumn();
                                    Text(Unsafe.Add<float>(ref Unsafe.As<T, float>(ref obj), i * cols + j).ToString("n1"));
                                }
                            }
                            EndTable();
                            SetWindowFontScale(1);
                        }
                    }
                }
                EndTable();
            }
        }

        AlignTextToFramePadding();
        Text("Mesh:"); SameLine();
        SetNextItemWidth(250);
        selectedMeshIndex += 1;
        if (Combo("##dropdown", ref selectedMeshIndex, meshNames, meshNames.Length))
            selectedVertexIndex = -1;
        selectedMeshIndex -= 1;
        if (SelectedMesh is not null)
        {
            SameLine(); Text(SelectedMesh.PrimitiveType.ToString());
        }

        if (SelectedMesh is not null)
        {
            Mesh mesh = SelectedMesh;
            bool prevDirty = mesh.Dirty, prevDirtyModel = mesh.DirtyModel;
            if (BeginTable("tableMeshProperties", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchSame))
            {
                string shaderPath = $"{Path.GetFileName(mesh.Shader.VertPath)}, {Path.GetFileName(mesh.Shader.FragPath)}";

                TableNextColumn(); Text($"Vertices: {mesh.Vertices.Count}");
                TableNextColumn(); Text($"Indexes: {mesh.Indexes.Count}");

                TableNextColumn(); AlignTextToFramePadding();
                Text($"Position:"); SameLine(); DragMeshVec3("##pos", mesh, ref mesh.PositionRef, 0.1f);
                TableNextColumn(); Text($"Origin:"); SameLine(); DragMeshVec3("##origin", mesh, ref mesh.OriginRef, 0.1f);

                TableNextColumn(); AlignTextToFramePadding();
                Text($"Rotation:"); SameLine(); DragEuler("##rot", mesh);
                TableNextColumn(); Text($"Texture: {Path.GetFileName(mesh.Texture?.Path) ?? "null"}");

                TableNextColumn(); Text($"Shader: {Path.GetFileName(mesh.Shader.VertPath)}");
                TableNextColumn(); Text($"Shader: {Path.GetFileName(mesh.Shader.FragPath)}");
                EndTable();
            }

            if (BeginTable("tableVertices", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))// | ImGuiTableFlags.ScrollY))
            {
                TableSetupColumn("Pos", ImGuiTableColumnFlags.WidthStretch);
                TableSetupColumn("Color", ImGuiTableColumnFlags.WidthStretch);
                TableSetupColumn("Tex");
                TableSetupColumn("TexId");
                TableHeadersRow();

                int selectedRangeLower = selectedVertexIndex / 3 * 3;
                int selectedRangeUpper = (selectedVertexIndex / 3 + 1) * 3;

                int count = mesh.VertexCount;
                if (selectedVertexIndex >= count)
                    selectedMeshIndex = -1;
                for (int i = 0; i <= count; i++)
                {
                    if (selectedVertexIndex != -1)
                    {
                        if (i - 1 == selectedVertexIndex)
                        {
                            // This affects the prev row
                            // Bg0, bg1 and cellbg are blended on top of each other if set, with alpha
                            TableSetBgColor(ImGuiTableBgTarget.RowBg0, (uint)Color.Pink.Darken(0.1f).ToArgb());
                        }
                        else if (i - 1 >= selectedRangeLower && i - 1 < selectedRangeUpper)
                        {
                            TableSetBgColor(ImGuiTableBgTarget.RowBg0, (uint)Color.Pink.Darken(0.5f).ToArgb());
                        }
                    }
                    if (i == count)
                    {
                        EndTable();
                        break;
                    }
                    TableNextRow();

                    Vertex v = mesh[i];
                    TableSetColumnIndex(0);
                    if (mesh == meshMe)
                        Text(v.Pos.ToString("n1"));
                    else
                    {
                        PushID(i * 2);
                        if (Button(v.Pos.ToString("n1"), new Vector2Net(-0.1f, 0)))
                            selectedVertexIndex = selectedVertexIndex == i ? -1 : i;
                        PopID();
                    }
                    TableSetColumnIndex(1);
                    Text(v.Color.ToString("n1"));
                    TableSetColumnIndex(2);
                    Text(v.Tex.ToString("n1"));
                    TableSetColumnIndex(3);
                    Text($"{v.TexId}");
                }
            }
            mesh.Dirty = prevDirty;
            mesh.DirtyModel = prevDirtyModel;
        }

        ImGui.End();
    }

    public void Draw()
    {
        if (selectedVertexIndex >= SelectedMesh?.VertexCount)
            selectedMeshIndex = -1;
        if (SelectedMesh is not null && SelectedMesh != meshMe)
        {
            GL.Disable(EnableCap.DepthTest);
            Mesh mesh = SelectedMesh;
            var colorVert = ColorUtils.ColorToVector(Color.Aqua);
            var colorPos = ColorUtils.ColorToVector(Color.RosyBrown);
            var colorOrigin = ColorUtils.ColorToVector(Color.Red);
            meshMe.Clear();
            uint vertIdx = 0;

            meshMe.Vertices.AddRange(DebugBall.Select(vert =>
            {
                vert.Pos += mesh.PositionRef;
                vert.Color = colorPos;
                vert.ColorW = 1;
                return vert;
            }));
            meshMe.Indexes.AddRange(DebugBallIndexes);
            vertIdx = (uint)meshMe.Vertices.Count;

            meshMe.Vertices.AddRange(DebugBall.Select(vert =>
            {
                vert.Pos += Vector3.Zero.ToWorld(mesh);
                vert.Color = colorOrigin;
                vert.ColorW = 1;
                return vert;
            }));
            meshMe.Indexes.AddRange(DebugBallIndexes.Select(i => i + vertIdx));
            vertIdx = (uint)meshMe.Vertices.Count;

            if (selectedVertexIndex >= 0)
            {
                Vertex vertTarget = mesh[selectedVertexIndex];
                Vector3 world = vertTarget.Pos.ToWorld(mesh);
                int selectedRangeLower = selectedVertexIndex / 3 * 3;
                int selectedRangeUpper = (selectedVertexIndex / 3 + 1) * 3;

                meshMe.Vertices.AddRange(DebugBall.Select(vert =>
                {
                    vert.Pos += world;
                    vert.Color = colorVert;
                    vert.ColorW = 1;
                    return vert;
                }));
                meshMe.Indexes.AddRange(DebugBallIndexes.Select(i => i + vertIdx));
                vertIdx = (uint)meshMe.Vertices.Count;

                for (int i = selectedRangeLower; i < selectedRangeUpper; i++)
                {
                    Vertex vert = mesh[i];
                    meshMe.Vertices.Add(new Vertex()
                    {
                        Pos = vert.Pos.ToWorld(mesh),
                        Color = colorVert,
                        ColorW = 0.5f,
                    });
                    meshMe.Indexes.Add((uint)meshMe.Vertices.Count - 1);
                }
            }

            meshMe.Draw();
            GL.Enable(EnableCap.DepthTest);
        }
    }

    public void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (SelectedMesh is not null)
        {
            selectedVertexIndex = Math.Clamp(selectedVertexIndex + -float.Sign(e.OffsetY),
                -1, SelectedMesh.VertexCount - 1);
        }
    }

    private static void DragMeshVec3(string label, Mesh mesh, ref Vector3 v, float v_speed)
    {
        ImGui.PushID(label);
        ImGui.SetNextItemWidth(140);
        if (DragFloat3(label, ref v.InterchangeRef(), v_speed, float.MinValue, float.MaxValue, "%.1f"))
            mesh.DirtyModel = true;
        ImGui.SameLine();
        if (ImGui.Button("x"))
        {
            v = Vector3.Zero;
            mesh.DirtyModel = true;
        }
        ImGui.PopID();
    }

    private static void DragEuler(string label, Mesh mesh)
    {
        ImGui.PushID(label);
        var rot = mesh.Rotation;
        ImGui.SetNextItemWidth(125);
        if (ImGui.DragFloat3(label, ref rot.InterchangeRef(), 1.0f, -180, 180, "%.1f"))
            mesh.Rotation = rot;

        ImGui.SameLine();
        if (ImGui.Button("x"))
            mesh.Rotation = Vector3.Zero;
        ImGui.PopID();
    }

    private static void DragQuatAsEulerAndW(string label, Mesh mesh, ref Quaternion q, float v_speed)
    {
        Vector3 e = q.ToEulerAngles() / MathF.PI * 180;
        if (DragFloat3(label + "euler", ref e.InterchangeRef(), 1f, -179.5f, 179.5f, "%.1f"))
        {
            q = Quaternion.FromEulerAngles(e / 180 * MathF.PI);
            q.Normalize();
            mesh.DirtyModel = true;
        }

        float deg = q.W * 180;
        AlignTextToFramePadding();
        Text("Quat W:"); SameLine();
        SetNextItemWidth(100);
        if (DragFloat(label + "quatw", ref deg, 1f, -179.5f, 179.5f, "%.1f"))
        {
            q.W = deg / 180;
            q.Normalize();
            mesh.DirtyModel = true;
        }
        SameLine();
        if (Button("Reset"))
        {
            q = Quaternion.Identity;
            mesh.DirtyModel = true;
        }
    }

    ~DebugRenderer()
    {
        Dispose();
    }

    public void Dispose()
    {
        meshMe.Dispose();
    }
}
