namespace AccelDrum.Game.Utils;

public static class BitUtils
{
    public static unsafe void ReverseBytewise<T>(ref T obj) where T : unmanaged
    {
        obj = ReverseBytewise(obj);
    }

    public static unsafe T ReverseBytewise<T>(T obj) where T : unmanaged
    {
        int size = sizeof(T);
        byte* ptr = (byte*)&obj;
        for (int i = 0; i < size / 2; i++)
        {
            byte t = ptr[i];
            ptr[i] = ptr[size - 1 - i];
            ptr[size - 1 - i] = t;
        }
        return obj;
    }
}
