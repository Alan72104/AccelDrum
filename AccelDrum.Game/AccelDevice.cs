using AccelDrum.Game.Accel;
using AccelDrum.Game.Extensions;
using AccelDrum.Game.Graphics;
using AccelDrum.Game.Serial;
using AccelDrum.Game.Utils;
using ImGuiNET;
using OpenTK.Mathematics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace AccelDrum.Game;

public class AccelDevice : IDisposable
{
    private string[] portNames = [];
    private SerialManager serial = new();
    private SerialPacket<AccelPacket>? latestDataNative = null;
    private AccelPacket? latestData = null;
    private Mesh meshTestCube = null!;
    private MeshManager meshManager = null!;
    private static Vector3 temp = new Vector3();
    private Vector3[] serialRotHistory = new Vector3[500];
    private Timer2 timer = new Timer2(1000 / 10);
    private Vector3 velocity = new();
    private SortedDictionary<AccelPacketType, int> packetCounts = new();
    private StringBuilder sbFromAccel = new();

    public AccelDevice(MeshManager meshManager)
    {
        this.meshManager = meshManager;

        meshTestCube = meshManager.CreateMesh("testCube", meshManager.Shaders["main"]);

        (meshTestCube.Vertices, meshTestCube.Indexes) = ShapeUtils.CubeWithTexture(1, 2, 1);
        meshTestCube.Texture = meshManager.Textures["container"];
        meshTestCube.Origin = new Vector3(0, 0, 1);
        meshTestCube.Position = new Vector3(0, 2, 0);
        DebugRenderer.Ins.SetMesh(meshTestCube);
    }

    public void Update()
    {
        serial.Update();

        if (serial.Connected)
            ReceivePackets();

        if (timer.CheckAndResetIfElapsed())
        {
            Array.ConstrainedCopy(serialRotHistory, 1, serialRotHistory, 0, serialRotHistory.Length - 1);
            serialRotHistory[serialRotHistory.Length - 1] = meshTestCube.Rotation;
        }
    }

    private void ReceivePackets()
    {
        while (serial.TryDequeueInboundNative(out SerialPacket native))
        {
            AccelPacketType type = (AccelPacketType)native.Type;
            if (packetCounts.ContainsKey(type))
                packetCounts[type]++;
            else
                packetCounts[type] = 1;
            switch (type)
            {
                case AccelPacketType.Accel:
                    {
                        AccelPacket p = native.GetInnerAs<AccelPacket>();
                        p = p with
                        {
                            Accel = ConvertAxes(p.Accel),
                            Gyro = ConvertAxes(p.Gyro),
                            GyroEuler = ConvertAxes(p.GyroEuler),
                        };
                        latestDataNative = SerialPacket<AccelPacket>.RefFromUntyped(ref native);
                        latestData = p;
                        if (p.DeltaMicros <= 1_000_000)
                        {
                            meshTestCube.RotationQuat = p.Gyro;
                            float secs = p.DeltaMicros / 1_000_000.0f;
                            velocity += p.Accel * secs;
                            meshTestCube.Position += p.Accel * secs * secs * 10;
                        }
                        break;
                    }
                case AccelPacketType.Text:
                    {
                        TextPacket p = native.GetInnerAs<TextPacket>();
                        if (p.Length > 0)
                        {
                            ReadOnlySpan<byte> span = MemoryMarshal.CreateReadOnlySpan(ref p.Str[0], (int)p.Length);
                            string str = Encoding.UTF8.GetString(span);
                            sbFromAccel.Append(str);
                            Console.WriteLine($"Text received len: {p.Length} content: {str}");
                            while (sbFromAccel.Length > 128)
                            {
                                int i = sbFromAccel.ToString().IndexOf('\n');
                                if (i == -1)
                                    break;
                                Console.WriteLine($"Removing {i} sb len: {sbFromAccel.Length    }");
                                if (sbFromAccel.Length > 150)
                                    sbFromAccel.Remove(0, sbFromAccel.Length - 150);
                                else
                                    sbFromAccel.Remove(0, i + 1);
                            }
                        }
                        break;
                    }
                default:
                    Console.WriteLine($"Unknown packet type {native.Type}");
                    break;
            }
        }
    }

    public void Draw()
    {
        meshTestCube.Draw();
    }

    public void SerialWindow()
    {
        if (ImGui.Begin("Serial"))
        {
            if (ImGui.Button("Get port names"))
                portNames = SerialPort.GetPortNames();
            foreach (string name in portNames)
            {
                ImGui.AlignTextToFramePadding();
                ImGui.Text(name);
                ImGui.SameLine();

                if (serial.Connected && serial.PortName == name)
                {
                    if (ImGui.Button("Close"))
                        serial.Disconnect();
                }
                else if (ImGui.Button("Connect"))
                {
                    if (serial.Connected)
                        serial.Disconnect();
                    serial.Connect(name, 1000000);
                }
            }

            ImGui.AlignTextToFramePadding();
            ImGui.Text("Reset");
            ImGui.SameLine();
            if (ImGui.Button("rotation"))
                meshTestCube.Rotation = Vector3.Zero;
            ImGui.SameLine();
            if (ImGui.Button("position & velocity"))
            {
                meshTestCube.Position = new Vector3(0, 2, 0);
                velocity = Vector3.Zero;
            }

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

            ImGui.Text("Packet counts");
            ImGui.SameLine();
            if (ImGui.BeginTable("tablePacketCount", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                foreach (var (type, val) in packetCounts)
                {
                    ImGui.TableNextColumn();
                    ImGui.Text($"{type}");
                    ImGui.TableNextColumn();
                    ImGui.Text($"{val:n0}");
                }
                ImGui.EndTable();
            }

            ImGui.Text($"Corrupted count: {serial.CorruptedPacketCount}");


            ImGui.AlignTextToFramePadding();
            ImGui.Text("String from accel");
            ImGui.SameLine();
            if (ImGui.Button("Clear"))
                sbFromAccel.Clear();
            string strFromAccel = sbFromAccel.ToString().Replace('\0', ' ');
            ImGui.Text(strFromAccel);

            if (serial.Connected)
            {
                if (latestData.HasValue)
                {

                    ImGui.Text("Latest packet");
                    if (ImGui.BeginTable("tableAccelData", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                    {
                        ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn("value");

                        ImGui.TableNextColumn();
                        ImGui.SetNextItemWidth(4);
                        ImGui.Text("Delta us");
                        ImGui.TableNextColumn();
                        ImGui.Text(latestData.Value.DeltaMicros.ToString());

                        ImGui.TableNextColumn();
                        ImGui.Text("m/s^2");
                        ImGui.TableNextColumn();
                        if (ImGui.BeginTable("tableAccelDataAccel", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                ImGui.TableNextColumn();
                                ImGui.Text(latestData.Value.Accel[i].ToString("n2"));
                            }
                            ImGui.EndTable();
                        }

                        ImGui.TableNextColumn();
                        ImGui.Text("quat");
                        ImGui.TableNextColumn();
                        if (ImGui.BeginTable("tableAccelDataQuat", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                        {
                            Quaternion quat = latestData.Value.Gyro;
                            for (int i = 0; i < 3; i++)
                            {
                                ImGui.TableNextColumn();
                                ImGui.Text(quat.Xyz[i].ToString("n2"));
                            }
                            ImGui.TableNextColumn();
                            ImGui.Text(quat.W.ToString("n2"));
                            ImGui.EndTable();
                        }

                        ImGui.TableNextColumn();
                        ImGui.Text("euler");
                        ImGui.TableNextColumn();
                        if (ImGui.BeginTable("tableAccelDataGyro", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                        {
                            Vector3 euler = latestData.Value.GyroEuler / MathF.PI * 180;
                            for (int i = 0; i < 3; i++)
                            {
                                ImGui.TableNextColumn();
                                ImGui.Text(euler[i].ToString("n2"));
                            }
                            ImGui.EndTable();
                        }

                        ImGui.TableNextColumn();
                        ImGui.Text("Crc32");
                        ImGui.TableNextColumn();
                        ImGui.Text($"0x{latestDataNative!.Value.Crc32:X}");

                        ImGui.TableNextColumn();
                        ImGui.Text("Magic");
                        ImGui.TableNextColumn();
                        ImGui.Text($"0x{serial.Magic:X}");
                        ImGui.EndTable();
                    }
                }
            }
            System.Numerics.Vector2 size = new(-1, 50);
            ImGui.PlotLines("x", ref serialRotHistory[0].X, serialRotHistory.Length, 0, null, -180, 180,
                size, Marshal.SizeOf<Vector3>());
            ImGui.PlotLines("y", ref serialRotHistory[0].Y, serialRotHistory.Length, 0, null, -180, 180,
                size, Marshal.SizeOf<Vector3>());
            ImGui.PlotLines("z", ref serialRotHistory[0].Z, serialRotHistory.Length, 0, null, -180, 180,
                size, Marshal.SizeOf<Vector3>());
            ImGui.End();
        }

        if (ImGui.Begin("Rot history"))
        {
            System.Numerics.Vector2 size = new(-1, (ImGui.GetContentRegionAvail().Y - ImGui.GetStyle().ItemInnerSpacing.Y * 2) / 3);
            ImGui.PlotLines("x", ref serialRotHistory[0].X, serialRotHistory.Length, 0, null, -180, 180,
                size, Marshal.SizeOf<Vector3>());
            ImGui.PlotLines("y", ref serialRotHistory[0].Y, serialRotHistory.Length, 0, null, -180, 180,
                size, Marshal.SizeOf<Vector3>());
            ImGui.PlotLines("z", ref serialRotHistory[0].Z, serialRotHistory.Length, 0, null, -180, 180,
                size, Marshal.SizeOf<Vector3>());
            ImGui.End();
        }
    }

    private static readonly Quaternion y_to_neg_x = Quaternion.FromAxisAngle(Vector3.UnitY, -MathHelper.PiOver2);
    private static readonly Quaternion z_to_y = Quaternion.FromAxisAngle(Vector3.UnitZ, MathHelper.PiOver2);
    private static readonly Quaternion x_to_neg_z = Quaternion.FromAxisAngle(Vector3.UnitX, -MathHelper.PiOver2);

    private static Quaternion ConvertAxes(Quaternion q)
    {
        //return (q * Quaternion.FromEulerAngles(new Vector3(-90, 90, -90) / 180 * MathF.PI)).Normalized();
        //return (Quaternion.FromEulerAngles(temp / 180 * MathF.PI) * q).Normalized();
        //return (y_to_neg_x * q * z_to_y * x_to_neg_z).Normalized();
        return new Quaternion(q.Y, -q.Z, -q.X, -q.W).Normalized();
        //return (q).Normalized();
        //return q;
    }

    private static Vector3 ConvertAxes(Vector3 v)
    {
        //return new Vector3(v);
        return new Vector3(v.Y, -v.X, v.Z);
        //return new Vector3(-v.Y, v.Z, -v.X);
    }

    ~AccelDevice()
    {
        Dispose();
    }

    public void Dispose()
    {
        serial.Dispose();
    }
}
