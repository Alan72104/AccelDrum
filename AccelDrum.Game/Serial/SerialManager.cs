using AccelDrum.Game.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    private ConcurrentQueue<SerialPacket> inboundQueue = new();
    private ulong lastLong = 0;
    //private ConcurrentQueue<SerialPacket> outboundPackets = new(); // Outbound queue?
    private volatile int bytesRead = 0;
    private byte[] outboundBuffer = new byte[SerialPacket.Size];

    public SerialManager()
    {
        if (SerialPacket.Size != SerialPacket.SizeExpected)
        {
            throw new NotSupportedException($"Packet size was modified {SerialPacket.SizeExpected} => {SerialPacket.Size}");
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
        inboundQueue.Clear();
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

    private bool TryEnqueueInbound(ref SerialPacket p)
    {
        uint crc = p.GetCrc32();
        if (crc != p.Crc32)
        {
            CorruptedPacketCount++;
            Console.WriteLine($"Crc32 doesn't match: 0x{crc:X} and 0x{p.Crc32:X}");
            return false;
        }
        inboundQueue.Enqueue(p);
        PacketCount++;
        return true;
    }

    public bool TryDequeueInbound<T>(out T outPacket) where T : struct
    {
        SerialPacket.CheckInnerSize<T>();

        if (!inboundQueue.TryDequeue(out SerialPacket packet))
        {
            outPacket = default;
            return false;
        }
        ref T inner = ref MemoryMarshal.AsRef<T>(packet.Inner);
        outPacket = inner;
        return true;
    }

    public bool TryDequeueInboundNative<T>(out SerialPacket<T> outPacket) where T : struct
    {
        SerialPacket.CheckInnerSize<T>();

        if (!inboundQueue.TryDequeue(out SerialPacket packet))
        {
            outPacket = default;
            return false;
        }
        ref SerialPacket<T> typed = ref SerialPacket<T>.RefFromUntyped(ref packet);
        outPacket = typed;
        return true;
    }

    public bool TryDequeueInboundNative(out SerialPacket outPacket)
    {
        if (!inboundQueue.TryDequeue(out outPacket))
        {
            return false;
        }
        return true;
    }

    public void SendPacket<T>(uint type, in T inner) where T : struct
    {
        SerialPacket.CheckInnerSize<T>();
        if (!Connected)
            throw new InvalidOperationException("Serial is not connected");

        ref SerialPacket<T> packet = ref Unsafe.As<byte, SerialPacket<T>>(ref outboundBuffer[0]);
        packet.Type = type;
        packet.Inner = inner;
        packet.Crc32 = packet.GetCrc32();
        packet.Magic = SerialPacket.MagicExpected;
        serial!.Write(outboundBuffer, 0, SerialPacket.Size);
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
