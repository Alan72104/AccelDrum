using AccelDrum.Game.Extensions;
using AccelDrum.Game.Graphics;
using AccelDrum.Game.Utils;
using ImGuiNET;
using ImuFusion;
using OpenTK.Mathematics;
using System;

namespace AccelDrum.Game.Accel;

public class AccelPart : IDisposable
{
    private SimpleFixedSizeHistoryQueue<Vector3> rotHistory = new(250);
    private Mesh meshTestCube = null!;
    private Vector3 velocity = new();
    private FusionAhrs fusion = null!;
    private FusionOffset fusionOffset = null!;
    private bool showRotHistory = false;

    public AccelPart(int x)
    {
        MeshManager meshManager = MeshManager.Ins;

        meshTestCube = meshManager.CreateMesh("testCube", meshManager.Shaders["main"]);

        (meshTestCube.Vertices, meshTestCube.Indexes) = ShapeUtils.CubeWithTexture(1, 2, 1);
        meshTestCube.Texture = meshManager.Textures["container"];
        meshTestCube.Origin = new Vector3(0, 0, 1);
        meshTestCube.Position = new Vector3(x, 2, 0);

        Reset();
    }

    public void Reset()
    {
        fusion = new FusionAhrs(new FusionAhrsSettings(
            gyroscopeRange: 1000.0f));
        fusionOffset = new(100);
    }

    public void Update()
    {
        rotHistory.Push(meshTestCube.Rotation);
    }

    public void Draw()
    {
        meshTestCube.Draw();
    }

    public void PushData(RawAccelPacket.Pack pack)
    {
        var secsElapsed = pack.DeltaMicros / 1_000_000.0f;
        var gyro = fusionOffset.Update(pack.Gyro.Interchange());
        fusion.UpdateNoMagnetometer(
            gyro,
            pack.Accel.Interchange(),
            secsElapsed);

        meshTestCube.Position += fusion.GetEarthAcceleration().Interchange() * secsElapsed * secsElapsed;
        meshTestCube.RotationQuat = fusion.Quaternion.Interchange();
    }

    public void DebugGui()
    {
        ImGui.Text("Cube vel");
        ImGui.SameLine();
        if (ImGui.BeginTable("tableCubeVel", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            for (int i = 0; i < 3; i++)
            {
                ImGui.TableNextColumn();
                ImGui.Text(velocity[i].ToString("n3"));
            }
            ImGui.EndTable();
        }
        ImGui.Text("Cube rot");
        ImGui.SameLine();
        if (ImGui.BeginTable("tableCubeRot", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            for (int i = 0; i < 3; i++)
            {
                ImGui.TableNextColumn();
                ImGui.Text(meshTestCube.Rotation[i].ToString("n2"));
            }
            ImGui.EndTable();
        }
        ImGui.Text("Cube pos");
        ImGui.SameLine();
        if (ImGui.BeginTable("tableCubePos", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            for (int i = 0; i < 3; i++)
            {
                ImGui.TableNextColumn();
                ImGui.Text(meshTestCube.Position[i].ToString("n3"));
            }
            ImGui.EndTable();
        }

        ImGui.Checkbox("Show rot history", ref showRotHistory);
        if (showRotHistory)
        {
            System.Numerics.Vector2 size = new(-1, 50);
            ImGui.PlotLines("x", ref rotHistory.Ref.X, rotHistory.Length, 0, null, -180, 180,
                size, rotHistory.ElementSize);
            ImGui.PlotLines("y", ref rotHistory.Ref.Y, rotHistory.Length, 0, null, -180, 180,
                size, rotHistory.ElementSize);
            ImGui.PlotLines("z", ref rotHistory.Ref.Z, rotHistory.Length, 0, null, -180, 180,
                size, rotHistory.ElementSize);
        }
    }

    ~AccelPart()
    {
        Dispose();
    }

    public void Dispose()
    {
        meshTestCube.Dispose();
    }
}
