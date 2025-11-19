using System;
using System.Globalization;
using System.Net.Sockets;

namespace UnityPerfProfilerWPF.Unity;

/// <summary>
/// Unity binary protocol utilities - exact port from UnityPerfProfiler
/// </summary>
public static class BinaryUtils
{
    public static unsafe bool UnsafeCompare(byte[] a1, byte[] a2)
    {
        if (a1 == a2)
        {
            return true;
        }
        if (a1 == null || a2 == null || a1.Length != a2.Length)
        {
            return false;
        }
        
        fixed (byte* p1 = a1, p2 = a2)
        {
            byte* x = p1;
            byte* x2 = p2;
            int i = a1.Length;
            int j = 0;
            while (j < i / 8)
            {
                if (*(long*)x != *(long*)x2)
                {
                    return false;
                }
                j++;
                x += 8;
                x2 += 8;
            }
            if ((i & 4) != 0)
            {
                if (*(int*)x != *(int*)x2)
                {
                    return false;
                }
                x += 4;
                x2 += 4;
            }
            if ((i & 2) != 0)
            {
                if (*(short*)x != *(short*)x2)
                {
                    return false;
                }
                x += 2;
                x2 += 2;
            }
            return (i & 1) == 0 || *x == *x2;
        }
    }

    public static string BinaryToHex(byte[] data)
    {
        return BitConverter.ToString(data).Replace("-", "");
    }

    public static bool ReadBytes(NetworkStream stream, byte[] buf)
    {
        int size = buf.Length;
        int rs;
        for (int offset = 0; offset < size; offset += rs)
        {
            rs = stream.Read(buf, offset, size - offset);
            if (rs == 0)
            {
                return false;
            }
        }
        return true;
    }

    public static string Reverse(string s)
    {
        char[] array = s.ToCharArray();
        Array.Reverse(array);
        return new string(array);
    }

    public static byte[] UnityGUID2Bytes(string unityGuid)
    {
        byte[] result = new byte[16];
        for (int i = 0; i < 4; i++)
        {
            string num = unityGuid.Substring(i * 8, 8);
            num = Reverse(num);
            uint j = 0U;
            for (int k = 0; k < 8; k += 2)
            {
                j = (j << 8) + uint.Parse(num.Substring(k, 2), NumberStyles.HexNumber);
            }
            BitConverter.GetBytes(j).CopyTo(result, i * 4);
        }
        return result;
    }

    public static byte[] SessionGUID2Bytes(string unityGuid)
    {
        byte[] result = new byte[16];
        for (int i = 0; i < 4; i++)
        {
            string num = unityGuid.Substring(i * 8, 8);
            num = Reverse(num);
            uint j = 0U;
            for (int k = 0; k < 8; k += 2)
            {
                char[] numArray = num.Substring(k, 2).ToCharArray();
                Array.Reverse(numArray);
                j = (j << 8) + uint.Parse(new string(numArray), NumberStyles.HexNumber);
            }
            BitConverter.GetBytes(j).CopyTo(result, i * 4);
        }
        return result;
    }
}