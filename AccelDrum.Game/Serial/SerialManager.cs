﻿using AccelDrum.Game.Accel;
using AccelDrum.Game.Utils;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Threading;

namespace AccelDrum.Game.Serial;

public class SerialManager : IDisposable
{
    public bool Connected => serial.IsOpen;
    public string PortName => Connected ? serial!.PortName : "";
    public ulong Magic => SerialPacket.MagicExpected;
    public int PacketCount { get; private set; } = 0;
    public int CorruptedPacketCount { get; private set; } = 0;
    public int BytesRead => bytesRead;
    private SerialPort serial = new();
    private Queue<byte> parsingQueue = new();
    private ConcurrentQueue<SerialPacket> inboundQueue = new();
    private ulong lastLong = 0;
    //private ConcurrentQueue<SerialPacket> outboundPackets = new(); // Outbound queue?
    private volatile int bytesRead = 0;
    private byte[] outboundBuffer = new byte[SerialPacket.Size];
    private Thread? receiverThread = null;
    private CancellationTokenSource receiverCancellationSource = new();

    public SerialManager()
    {
        if (SerialPacket.Size != SerialPacket.SizeExpected)
        {
            throw new NotSupportedException($"Packet size was modified {SerialPacket.SizeExpected} => {SerialPacket.Size}");
        }
        serial.ErrorReceived += OnSerialErrorReceived;
    }

    public void Update()
    {
    }

    public void Connect(string name, int baud)
    {
        if (Connected)
            throw new InvalidOperationException("Serial is already connected");
        serial.PortName = name;
        serial.BaudRate = baud;
        serial.Open();
        receiverCancellationSource = new();
        receiverThread = new Thread(ReceiveThread);
        receiverThread.Name = "SerialReceiver";
        receiverThread.IsBackground = true;
        receiverThread.Priority = ThreadPriority.AboveNormal;
        Tuple<SerialPort, CancellationToken> param = new(serial, receiverCancellationSource.Token);
        receiverThread.Start(param);
    }

    public void Disconnect()
    {
        if (!Connected)
            throw new InvalidOperationException("Serial is already disconnected");
        receiverCancellationSource.Cancel();
        serial.Close();
        receiverThread!.Join();
        receiverThread = null;
        parsingQueue.Clear();
        lastLong = 0;
        inboundQueue.Clear();
        PacketCount = 0;
        CorruptedPacketCount = 0;
        bytesRead = 0;
    }

    public string[] GetPortNames()
    {
        return SerialPort.GetPortNames();
    }

    private void ReceiveThread(object? param)
    {
        (SerialPort serial, CancellationToken cancellationToken) = (Tuple<SerialPort, CancellationToken>)param!;
        while (!cancellationToken.IsCancellationRequested)
        {
            OnSerialDataReceived(serial);
            Thread.Sleep(1);
            Thread.Yield();
        }
    }

    private void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        OnSerialDataReceived(sender);
    }

    private void OnSerialDataReceived(object sender)
    {
        SerialPort serial = (SerialPort)sender;
        try
        {
            while (serial.BytesToRead > 0 && serial.IsOpen)
            {
                int @int = (byte)serial.ReadByte();
                if (@int == -1)
                    return;
                byte b = (byte)@int;
                parsingQueue.Enqueue(b);
                lastLong <<= 8; // Data is little endian, and gets reversed by <<
                lastLong |= b;
                bytesRead++;
                if (parsingQueue.Count > SerialPacket.Size)
                    parsingQueue.Dequeue();
                if (parsingQueue.Count == SerialPacket.Size &&
                    lastLong == SerialPacket.MagicExpectedReversed)
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
        catch (Exception e)
        {
            Log.Warning($"{nameof(OnSerialDataReceived)}: {e.Message}", e);
        }
    }

    private void OnSerialErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        SerialPort serial = (SerialPort)sender;
        serial.DiscardInBuffer();
        parsingQueue.Clear();
        lastLong = 0;
        Log.Warning($"Serial errored: {e.EventType}");
    }

    private bool TryEnqueueInbound(ref SerialPacket p)
    {
        uint crc = p.GetCrc32();
        if (crc != p.Crc32)
        {
            CorruptedPacketCount++;
            Log.Warning($"Crc32 doesn't match: 0x{crc:X} and 0x{p.Crc32:X}");
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
        ref SerialPacket<T> typed = ref SerialPacket<T>.RefFromUntyped(ref packet);
        outPacket = typed.Inner;
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

    public void SendPacket<T>(PacketType type, in T inner) where T : struct
    {
        SerialPacket.CheckInnerSize<T>();
        if (!Connected)
            throw new InvalidOperationException("Serial is not connected");

        ref SerialPacket<T> packet = ref Unsafe.As<byte, SerialPacket<T>>(ref outboundBuffer[0]);
        packet.Type = (uint)type;
        packet.Inner = inner;
        packet.Crc32 = packet.GetCrc32();
        packet.Magic = SerialPacket.MagicExpected;
        using (new Timer2(
            time => Log.Information($"Packet of type {typeof(T).Name} sent in {time.TotalMicroseconds:n0} us " +
                $"(eff. {SerialPacket.Size * 8 / time.TotalSeconds:n0} bit/s)")))
        {
            serial.Write(outboundBuffer, 0, SerialPacket.Size);
        }
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
