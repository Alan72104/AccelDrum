using AccelDrum.Game.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Hashing;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AccelDrum.Game.Serial;

public class SerialManager : IDisposable
{

    public bool Connected => serial is not null;
    public string PortName => Connected ? serial!.PortName : "";
    public ulong Magic => SerialPacket.MagicExpected;
    public int PacketCount { get; private set; } = 0;
    public int CorruptedPacketCount { get; private set; } = 0;
    public int BytesRead => bytesRead;
    private SerialPort? serial;
    private Queue<byte> parsingQueue = new();
    private ulong lastLong = 0;
    private ConcurrentQueue<SerialPacket> inboundPackets = new();
    private volatile int bytesRead = 0;
    private Crc32 crcIn = new();

    public SerialManager()
    {
        if (SerialPacket.Size != SerialPacket.SizeExpected)
        {
            throw new NotSupportedException($"Struct size was modified {SerialPacket.SizeExpected} => {SerialPacket.Size}");
        }
    }

    public void Update()
    {
        if (Connected && !serial!.IsOpen)
            Disconnect();
    }

    public void Connect(string name, int baud)
    {
        if (Connected)
            throw new InvalidOperationException("Serial is already connected");
        serial = new SerialPort()
        {
            PortName = name,
            BaudRate = baud,
            ReadTimeout = 100,
            WriteTimeout = 250,
        };
        serial.DataReceived += OnSerialDataReceived;
        serial.ErrorReceived += OnSerialErrorReceived;
        serial.Open();
    }

    public void Disconnect()
    {
        if (!Connected)
            throw new InvalidOperationException("Serial is already disconnected");
        //serial!.DataReceived -= OnSerialDataReceived;
        //serial!.ErrorReceived -= OnSerialErrorReceived;
        serial!.Close();
        serial = null;
        inboundPackets.Clear();
        PacketCount = 0;
        CorruptedPacketCount = 0;
        bytesRead = 0;
    }

    private void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        SerialPort serial = (SerialPort)sender;
        try
        {
            while (serial.BytesToRead > 0)
            {
                int @int = (byte)serial.ReadByte();
                if (@int == -1)
                    return;
                byte b = (byte)@int;
                parsingQueue.Enqueue(b);
                lastLong <<= 8; // Data is little endian, we reverse it later for readability
                lastLong |= b;
                bytesRead++;
                if (parsingQueue.Count > SerialPacket.Size)
                    parsingQueue.Dequeue();
                if (parsingQueue.Count == SerialPacket.Size &&
                    BitUtils.ReverseBytewise(lastLong) == SerialPacket.MagicExpected)
                {
                    SerialPacket newPacket = new();
                    unsafe
                    {
                        for (int i = 0; i < SerialPacket.Size; i++)
                            ((byte*)&newPacket)[i] = parsingQueue.Dequeue();
                    }
                    TryEnqueueInbound(ref newPacket);
                }
            }
        }
        catch (Exception) { }
    }

    private void OnSerialErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        SerialPort serial = (SerialPort)sender;
        serial.DiscardInBuffer();
        parsingQueue.Clear();
        lastLong = 0;
        Console.WriteLine($"Serial errored: {e.EventType}");
    }

    private Timer2 t = new Timer2(500).Start();

    private bool TryEnqueueInbound(ref SerialPacket p)
    {
        unsafe
        {
            fixed (byte* ptr = p.Inner)
            {
                byte[] hash = Crc32.Hash(new ReadOnlySpan<byte>(ptr, SerialPacket.SizeInner));
                uint crc = BitConverter.ToUInt32(hash);
                if (crc != p.Crc32)
                {
                    CorruptedPacketCount++;
                    if (t.CheckAndResetIfElapsed())
                    {
                        Console.WriteLine($"Crc32 doesn't match: 0x{crc:X} and 0x{p.Crc32:X}");
                    }
                    return false;
                }
            }
        }
        inboundPackets.Enqueue(p);
        PacketCount++;
        return true;
    }

    public bool TryDequeueInbound<T>(out T packet) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        if (size != SerialPacket.SizeInner)
            throw new InvalidOperationException($"Inner packet size should be {SerialPacket.SizeInner} but is {size}");

        if (!inboundPackets.TryDequeue(out SerialPacket serialPacket))
        {
            packet = default;
            return false;
        }
        unsafe
        {
            ref T inner = ref Unsafe.AsRef<T>(serialPacket.Inner);
            packet = inner;
        }
        return true;
    }

    public bool TryDequeueInboundNative<T>(out SerialPacket<T> packet) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        if (size != SerialPacket.SizeInner)
            throw new InvalidOperationException($"Inner packet size should be {SerialPacket.SizeInner} but is {size}");

        if (!inboundPackets.TryDequeue(out SerialPacket serialPacket))
        {
            packet = default;
            return false;
        }
        unsafe
        {
            ref SerialPacket<T> inner = ref Unsafe.As<SerialPacket, SerialPacket<T>>(ref serialPacket);
            packet = inner;
        }
        return true;
    }

    ~SerialManager()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (Connected)
        {
            Disconnect();
        }
    }
}
