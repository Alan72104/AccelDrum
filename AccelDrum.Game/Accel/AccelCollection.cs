using AccelDrum.Game.Serial;
using AccelDrum.Game.Utils;
using ImGuiNET;
using OpenTK.Mathematics;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AccelDrum.Game.Accel;

public class AccelCollection : IDisposable
{
    public bool Connected => serial.Connected;

    private string[] portNames = [];
    private SerialManager serial = new();
    private bool dataArrived = false;
    private SerialPacket<RawAccelPacket> latestAccelPacketNative;
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

    private AccelPart[]? accelDevices = null;
    private Timer2 accelPollTimer = new(750);

    public AccelCollection()
    {
    }

    public void Update()
    {
        serial.Update();

        if (Connected)
        {
            if (accelPollTimer.CheckAndResetIfElapsed())
            {
                serial.SendPacket(PacketType.Configure, new ConfigurePacket(
                    ConfigurePacket.Typ.PollForData, ConfigurePacket.Val.None));
            }
            ReceivePackets();
        }
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
    }

    private void HandlePacketRawAccel(RawAccelPacket p)
    {
        int i = 0;
        foreach (RawAccelPacket.Pack packNat in p.Packs)
        {
            var pack = packNat with
            {
                Accel = ConvertAxes(packNat.Accel),
                Gyro = ConvertAxes(packNat.Gyro),
            };
            dataArrived = true;
            if (pack.DeltaMicros <= 1_000_000 / 100 * 2)
            {
                accelDevices![i].PushData(pack);
            }
            i++;
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
        if (accelDevices is not null)
        {
            foreach (var dev in accelDevices)
            {
                dev.Draw();
            }
        }
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

        accelDevices = new[]
        {
            new AccelPart(-3),
            new AccelPart(-1),
            new AccelPart(1),
            new AccelPart(3),
        };
    }

    private void Disconnect()
    {
        using (new Timer2(
            time => Log.Information($"Disconnection took {time.TotalMicroseconds:n0} us")))
        {
            serial.Disconnect();
        }

        foreach (var dev in accelDevices!)
            dev.Dispose(); // Dispose opengl buffers in the main thread
        accelDevices = null;
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

                    if (Connected && serial.PortName == name)
                    {
                        if (ImGui.Button("Close"))
                            Disconnect();
                    }
                    else if (ImGui.Button("Connect"))
                    {
                        if (Connected)
                            Disconnect();
                        Connect(name);
                    }
                }
                ImGui.EndTable();
            }

            if (Connected)
            {
                if (ImGui.Button("Reset all"))
                {
                    foreach (var dev in accelDevices!)
                        dev.Reset();
                }

                if (ImGui.BeginTabBar("tabbarDevices"))
                {
                    int i = 0;
                    foreach (var dev in accelDevices!)
                    {
                        if (ImGui.BeginTabItem((++i).ToString()))
                        {
                            dev.DebugGui();
                            ImGui.EndTabItem();
                        }
                    }
                    ImGui.EndTabBar();
                }
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

            if (Connected && dataArrived)
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
                    foreach (var dev in accelDevices!)
                        dev.Reset();
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

    ~AccelCollection()
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
