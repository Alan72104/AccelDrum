using System;
using System.Runtime.CompilerServices;

namespace AccelDrum.Game.Utils;

public class SimpleFixedSizeHistoryQueue<T>(int size)
{
    public int Length => array.Length;
    public int ElementSize => Unsafe.SizeOf<T>();
    public ref T Ref => ref array[0];

    private readonly T[] array = new T[size];

    public void Push(T ele)
    {
        Array.ConstrainedCopy(array, 1, array, 0, Length - 1);
        array[Length - 1] = ele;
    }

    public void Clear()
    {
        Array.Clear(array, 0, Length);
    }
}
