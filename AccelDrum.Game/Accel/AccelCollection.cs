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
    private bool showAccelSettings = false;
    private bool showPacketBytes = false;
    private bool showLatestAccelPacket = false;

    private AccelPart[]? accelDevices = null;
    private Timer2 accelPollTimer = new(750);
    private AccelSettings? accelSettings = null;

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
                    HandlePacketConfigure(native.GetInnerAs<ConfigurePacket>());
                    break;
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
                Accel = ConvertAxesv(packNat.Accel),
                Gyro = ConvertAxesv(packNat.Gyro),
            };
            dataArrived = true;
            if (pack.DeltaMicros <= 1_000_000 / 100 * 2)
            {
                accelDevices![i].PushData(pack.DeltaMicros / 1_000_000.0f, pack.Accel, pack.Gyro);
            }
            i++;
        }

        static Quaternion ConvertAxes(Quaternion q)
        {
            return new Quaternion(q.Y, -q.Z, -q.X, -q.W).Normalized();
        }

        static Vector3 ConvertAxesv(Vector3 v)
        {
            return new Vector3(v.Y, -v.X, v.Z);
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

    private void HandlePacketConfigure(ConfigurePacket p)
    {
        if (p.Type == ConfigurePacket.Typ.Reset &&
            p.Value == ConfigurePacket.Val.ResetResultSettings)
        {
            var settings = p.GetDataAs<ConfigurePacket.Settings>();
            var a1 = new Vector3i[4];
            var a2 = new Vector3i[4];
            for (int i = 0; i < 4; i++)
            {
                a1[i] = new Vector3i(settings.AccelFactoryTrims[i],
                    settings.AccelFactoryTrims[i + 1], settings.AccelFactoryTrims[i + 2]);
                a2[i] = new Vector3i(settings.GyroFactoryTrims[i],
                    settings.GyroFactoryTrims[i + 1], settings.GyroFactoryTrims[i + 2]);
            }
            accelSettings = new()
            {
                AccelRange = (AccelFullScale)settings.AccelRange,
                GyroRange = (GyroFullScale)settings.GyroRange,
                AccelFactoryTrims = a1,
                GyroFactoryTrims = a2,
            };
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

        serial.SendPacket(PacketType.Configure, new ConfigurePacket(
            ConfigurePacket.Typ.Reset, ConfigurePacket.Val.None)); // Retrieve the settings at start

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
            dev.Dispose(); // Dispose OpenGL buffers in the main thread
        accelDevices = null;
        accelSettings = null;
        dataArrived = false;
    }

    public void SerialWindow()
    {
        if (ImGui.Begin("serial"))
        {
            if (ImGui.Button("get port names"))
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
                        if (ImGui.Button("close"))
                            Disconnect();
                    }
                    else if (ImGui.Button("connect"))
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
                if (ImGui.Button("reset all"))
                {
                    foreach (var dev in accelDevices!)
                        dev.Reset();
                }

                if (ImGui.BeginTabBar("tabBarDevices"))
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
                    ImGui.Separator();
                }
            }

            ImGui.Text("packet counts");
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

            ImGui.Text($"corrupted count: {serial.CorruptedPacketCount}");

            if (Connected && dataArrived)
            {
                if (ImGui.Button("backlight toggle"))
                {
                    serial.SendPacket(PacketType.Configure, new ConfigurePacket(
                        ConfigurePacket.Typ.Backlight, ConfigurePacket.Val.BacklightSetToggle));
                }
                ImGui.SameLine();
                if (ImGui.Button("on"))
                {
                    serial.SendPacket(PacketType.Configure, new ConfigurePacket(
                        ConfigurePacket.Typ.Backlight, ConfigurePacket.Val.BacklightSetOn));
                }
                ImGui.SameLine();
                if (ImGui.Button("off"))
                {
                    serial.SendPacket(PacketType.Configure, new ConfigurePacket(
                        ConfigurePacket.Typ.Backlight, ConfigurePacket.Val.BacklightSetOff));
                }
                ImGui.SameLine();
                if (ImGui.Button("reset"))
                {
                    serial.SendPacket(PacketType.Configure, new ConfigurePacket(
                        ConfigurePacket.Typ.Reset, ConfigurePacket.Val.None));
                    foreach (var dev in accelDevices!)
                        dev.Reset();
                }

                ImGui.AlignTextToFramePadding();
                ImGui.Text("strings from accel");
                ImGui.SameLine();
                if (ImGui.Button("clear"))
                {
                    stringsFromAccel.Clear();
                    stringsFromAccelFull = "";
                }
                ImGui.Text(stringsFromAccelFull);

                ImGui.Checkbox("accel settings", ref showAccelSettings);
                if (showAccelSettings && accelSettings is not null)
                {
                    ImGui.Indent();
                    ImGui.Text($"accel range: {accelSettings.AccelRange}");
                    ImGui.Text($"gyro range: {accelSettings.GyroRange}");
                    ImGui.Text($"accel factory trims:");
                    ImGui.Indent();
                    foreach (Vector3i v in accelSettings.AccelFactoryTrims)
                        ImGui.Text(v.ToString());
                    ImGui.Unindent();
                    ImGui.Text($"gyro factory trims:");
                    ImGui.Indent();
                    foreach (Vector3i v in accelSettings.GyroFactoryTrims)
                        ImGui.Text(v.ToString());
                    ImGui.Unindent();
                    ImGui.Unindent();
                }

                ImGui.Checkbox("packet delay", ref showPacketTime);
                if (showPacketTime)
                {
                    ImGui.PlotLines("##packetDelay", ref packetTimeHistory.Ref, packetTimeHistory.Length, 0, null, 0, 100,
                        new(-1, 50), packetTimeHistory.ElementSize);
                    packetTimePlotHovered = ImGui.IsItemHovered();
                }

                ImGui.Checkbox("show bytes", ref showPacketBytes);
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

                ImGui.Checkbox("show latest accel packet", ref showLatestAccelPacket);
                if (showLatestAccelPacket)
                {
                    RawAccelPacket.Pack accel = latestAccelPacketNative.Inner.Packs[0];
                    if (ImGui.BeginTable("tableAccelData", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                    {
                        ImGui.TableSetupColumn("name", ImGuiTableColumnFlags.WidthFixed);
                        ImGui.TableSetupColumn("value");

                        ImGui.TableNextColumn();
                        ImGui.SetNextItemWidth(4);
                        ImGui.Text("delta us");
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
                        ImGui.Text("crc32");
                        ImGui.TableNextColumn();
                        ImGui.Text($"0x{latestAccelPacketNative.Crc32:X}");

                        ImGui.TableNextColumn();
                        ImGui.Text("magic");
                        ImGui.TableNextColumn();
                        ImGui.Text($"0x{latestAccelPacketNative.Magic:X}");
                        ImGui.EndTable();
                    }
                }
            }
            ImGui.End();
        }
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
