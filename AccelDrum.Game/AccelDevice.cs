﻿using AccelDrum.Game.Accel;
using AccelDrum.Game.Extensions;
using AccelDrum.Game.Graphics;
using AccelDrum.Game.Serial;
using AccelDrum.Game.Utils;
using ImGuiNET;
using ImuFusion;
using OpenTK.Mathematics;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AccelDrum.Game;

public class AccelDevice : IDisposable
{
    private string[] portNames = [];
    private SerialManager serial = new();
    private bool dataArrived = false;
    private SerialPacket<RawAccelPacket> latestAccelPacketNative;
    private SimpleFixedSizeHistoryQueue<Vector3> rotHistory = new(250);
    private SimpleFixedSizeHistoryQueue<float> packetTimeHistory = new(250);
    private Stopwatch packetTimer = new();
    private SortedDictionary<PacketType, int> packetCounts = new();
    private List<string> stringsFromAccel = new();
    private string stringsFromAccelFull = "";
    private List<byte> sbFromAccel = new();
    private bool showPacketTime = true;
    private bool packetTimePlotHovered = false;
    private bool showPacketBytes = false;
    private bool showLatestAccelPacket = false;
    private bool showRotHistory = false;

    private Mesh meshTestCube = null!;
    private MeshManager meshManager = null!;
    private Vector3 velocity = new();
    private FusionAhrs ahrs = null!;
    private FusionOffset fusionOffset = null!;

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

        rotHistory.Push(meshTestCube.Rotation);
    }

    private void ReceivePackets()
    {
        while (serial.TryDequeueInboundNative(out SerialPacket native))
        {
            PacketType type = (PacketType)native.Type;
            if (packetCounts.ContainsKey(type))
                packetCounts[type]++;
            else
                packetCounts[type] = 1;
            if (!packetTimePlotHovered)
                packetTimeHistory.Push(packetTimer.ElapsedMilliseconds);
            packetTimer.Restart();
            switch (type)
            {
                case PacketType.Accel:
                    HandlePacketAccel(native.GetInnerAs<AccelPacket>());
                    break;
                case PacketType.RawAccel:
                    latestAccelPacketNative = SerialPacket<RawAccelPacket>.RefFromUntyped(ref native);
                    HandlePacketRawAccel(native.GetInnerAs<RawAccelPacket>());
                    break;
                case PacketType.Text:
                    HandlePacketText(native.GetInnerAs<TextPacket>());
                    break;
                case PacketType.Configure:
                    {
                        ConfigurePacket p = native.GetInnerAs<ConfigurePacket>();
                        Log.Information($"Received config packet: {p.Value}");
                        break;
                    }
                default:
                    Log.Warning($"Unknown packet type {native.Type}");
                    break;
            }
        }
    }

    private void HandlePacketAccel(AccelPacket p)
    {
        p = p with
        {
            Accel = ConvertAxes(p.Accel),
            Gyro = ConvertAxes(p.Gyro),
            GyroEuler = ConvertAxes(p.GyroEuler),
        };
        if (p.DeltaMicros <= 1_000_000)
        {
            meshTestCube.RotationQuat = p.Gyro;
            float secs = p.DeltaMicros / 1_000_000.0f;
            velocity += p.Accel * secs;
            meshTestCube.Position += p.Accel * secs * secs * 10;
        }
    }

    private void HandlePacketRawAccel(RawAccelPacket p)
    {
        foreach (RawAccelPacket.Pack packNat in p.Packs)
        {
            var pack = packNat with
            {
                Accel = ConvertAxes(packNat.Accel),
                Gyro = ConvertAxes(packNat.Gyro),
            };
            dataArrived = true;
            //if (pack.DeltaMicros <= 1_000_000)
            //{
            //    meshTestCube.Rotation = pack.Gyro;
            //    float secs = pack.DeltaMicros / 1_000_000.0f;
            //    velocity += pack.Accel * secs;
            //meshTestCube.Position += pack.Accel * secs * secs * 10;
            //}
            if (pack.DeltaMicros <= 1_000_000 / 100)
            {
                var secsElapsed = pack.DeltaMicros / 1_000_000.0f;
                var gyro = fusionOffset.Update(pack.Gyro.Interchange());
                ahrs.UpdateNoMagnetometer(
                    gyro,
                    pack.Accel.Interchange(),
                    secsElapsed);

                meshTestCube.Position += ahrs.GetEarthAcceleration().Interchange() * secsElapsed * secsElapsed;
                meshTestCube.RotationQuat = ahrs.Quaternion.Interchange();
            }
        }
    }

    private void HandlePacketText(TextPacket p)
    {
        if (p.Length > 0)
        {
            ReadOnlySpan<byte> span = MemoryMarshal.CreateReadOnlySpan(ref p.Str[0], (int)p.Length);
            if (p.HasNext)
            {
                sbFromAccel.AddRange(span);
            }
            else
            {
                if (sbFromAccel.Count > 0)
                {
                    sbFromAccel.AddRange(span);
                    string str = Encoding.UTF8.GetString(sbFromAccel.ToArray()).Replace('\0', ' ');
                    stringsFromAccel.Add(str);
                    sbFromAccel.Clear();
                }
                else
                {
                    string str = Encoding.UTF8.GetString(sbFromAccel.ToArray()).Replace('\0', ' ');
                    stringsFromAccel.Add(str);
                }
                if (stringsFromAccel.Count > 5)
                    stringsFromAccel.RemoveAt(0);
                stringsFromAccelFull = string.Join("", stringsFromAccel);
            }
        }
    }

    public void Draw()
    {
        meshTestCube.Draw();
    }

    private void Connect(string name)
    {
        using (new Timer2(
            time => Log.Information($"Connection took {time.TotalMicroseconds:n0} us")))
        {
            serial.Connect(name, 1_000_000);
        }
        packetTimeHistory.Clear();
        packetTimer.Restart();

        ahrs = new FusionAhrs(new FusionAhrsSettings(
            gyroscopeRange: 1000.0f));
        fusionOffset = new(100);
    }

    private void Disconnect()
    {
        using (new Timer2(
            time => Log.Information($"Disconnection took {time.TotalMicroseconds:n0} us")))
        {
            serial.Disconnect();
        }
    }

    public void SerialWindow()
    {
        if (ImGui.Begin("Serial"))
        {
            if (ImGui.Button("Get port names"))
                portNames = serial.GetPortNames();
            ImGui.SameLine();
            if (ImGui.BeginTable("tablePorts", 1, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                foreach (string name in portNames)
                {
                    ImGui.TableNextColumn();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text(name);
                    ImGui.SameLine();

                    if (serial.Connected && serial.PortName == name)
                    {
                        if (ImGui.Button("Close"))
                            Disconnect();
                    }
                    else if (ImGui.Button("Connect"))
                    {
                        if (serial.Connected)
                            Disconnect();
                        Connect(name);
                    }
                }
                ImGui.EndTable();
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
            ImGui.Text("Strings from accel");
            ImGui.SameLine();
            if (ImGui.Button("Clear"))
            {
                stringsFromAccel.Clear();
                stringsFromAccelFull = "";
            }
            ImGui.Text(stringsFromAccelFull);

            if (serial.Connected && dataArrived)
            {
                if (ImGui.Button("Backlight toggle"))
                {
                    serial.SendPacket(PacketType.Configure, new ConfigurePacket(
                        ConfigurePacket.Typ.Backlight, ConfigurePacket.Val.BacklightSetToggle));
                }
                ImGui.SameLine();
                if (ImGui.Button("On"))
                {
                    serial.SendPacket(PacketType.Configure, new ConfigurePacket(
                        ConfigurePacket.Typ.Backlight, ConfigurePacket.Val.BacklightSetOn));
                }
                ImGui.SameLine();
                if (ImGui.Button("Off"))
                {
                    serial.SendPacket(PacketType.Configure, new ConfigurePacket(
                        ConfigurePacket.Typ.Backlight, ConfigurePacket.Val.BacklightSetOff));
                }
                ImGui.SameLine();
                if (ImGui.Button("Reset dmp"))
                {
                    serial.SendPacket(PacketType.Configure, new ConfigurePacket(
                        ConfigurePacket.Typ.ResetDmp, ConfigurePacket.Val.None));
                }

                ImGui.Checkbox("Packet delay", ref showPacketTime);
                if (showPacketTime)
                {
                    ImGui.PlotLines("##packetDelay", ref packetTimeHistory.Ref, packetTimeHistory.Length, 0, null, 0, 100,
                        new(-1, 50), packetTimeHistory.ElementSize);
                    packetTimePlotHovered = ImGui.IsItemHovered();
                }

                ImGui.Checkbox("Show bytes", ref showPacketBytes);
                if (showPacketBytes)
                {
                    if (ImGui.BeginTable("tablePacketBytes", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                    {
                        ImGui.PushFont(ImGuiController.FontSourceCodePro);
                        SerialPacket p = SerialPacket.RefFromTyped(ref latestAccelPacketNative);
                        foreach (string s in SerialPacket.GetBytesHex(ref p, 4))
                        {
                            ImGui.TableNextColumn();
                            ImGui.Text(s);
                        }
                        ImGui.EndTable();
                        ImGui.PopFont();
                    }
                }

                ImGui.Checkbox("Show latest accel packet", ref showLatestAccelPacket);
                if (showLatestAccelPacket)
                {
                    RawAccelPacket.Pack accel = latestAccelPacketNative.Inner.Packs[0];
                    if (ImGui.BeginTable("tableAccelData", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                    {
                        ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn("value");

                        ImGui.TableNextColumn();
                        ImGui.SetNextItemWidth(4);
                        ImGui.Text("Delta us");
                        ImGui.TableNextColumn();
                        ImGui.Text(accel.DeltaMicros.ToString());

                        ImGui.TableNextColumn();
                        ImGui.Text("m/s^2");
                        ImGui.TableNextColumn();
                        if (ImGui.BeginTable("tableAccelDataAccel", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                ImGui.TableNextColumn();
                                ImGui.Text(accel.Accel[i].ToString("n2"));
                            }
                            ImGui.EndTable();
                        }

                        //ImGui.TableNextColumn();
                        //ImGui.Text("quat");
                        //ImGui.TableNextColumn();
                        //if (ImGui.BeginTable("tableAccelDataQuat", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                        //{
                        //    Quaternion quat = accel.Gyro;
                        //    for (int i = 0; i < 3; i++)
                        //    {
                        //        ImGui.TableNextColumn();
                        //        ImGui.Text(quat.Xyz[i].ToString("n2"));
                        //    }
                        //    ImGui.TableNextColumn();
                        //    ImGui.Text(quat.W.ToString("n2"));
                        //    ImGui.EndTable();
                        //}

                        ImGui.TableNextColumn();
                        ImGui.Text("euler");
                        ImGui.TableNextColumn();
                        if (ImGui.BeginTable("tableAccelDataGyro", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                        {
                            //Vector3 euler = accel.GyroEuler / MathF.PI * 180;
                            Vector3 euler = accel.Gyro;
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
                        ImGui.Text($"0x{latestAccelPacketNative.Crc32:X}");

                        ImGui.TableNextColumn();
                        ImGui.Text("Magic");
                        ImGui.TableNextColumn();
                        ImGui.Text($"0x{latestAccelPacketNative.Magic:X}");
                        ImGui.EndTable();
                    }
                }
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

            ImGui.End();
        }

        if (ImGui.Begin("Rot history"))
        {
            System.Numerics.Vector2 size = new(-1, (ImGui.GetContentRegionAvail().Y - ImGui.GetStyle().ItemInnerSpacing.Y * 2) / 3);
            ImGui.PlotLines("x", ref rotHistory.Ref.X, rotHistory.Length, 0, null, -180, 180,
                size, rotHistory.ElementSize);
            ImGui.PlotLines("y", ref rotHistory.Ref.Y, rotHistory.Length, 0, null, -180, 180,
                size, rotHistory.ElementSize);
            ImGui.PlotLines("z", ref rotHistory.Ref.Z, rotHistory.Length, 0, null, -180, 180,
                size, rotHistory.ElementSize);
            ImGui.End();
        }
    }

    private static Quaternion ConvertAxes(Quaternion q)
    {
        return new Quaternion(q.Y, -q.Z, -q.X, -q.W).Normalized();
    }

    private static Vector3 ConvertAxes(Vector3 v)
    {
        return new Vector3(v.Y, -v.X, v.Z);
    }

    ~AccelDevice()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (serial.Connected)
            Disconnect();
        serial.Dispose();
    }
}
