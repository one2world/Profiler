using System;
using System.Numerics;

namespace Unity.Mathematics
{
    public static class math
    {
        public static int max(int a, int b) => Math.Max(a, b);
        public static long max(long a, long b) => Math.Max(a, b);
        public static int min(int a, int b) => Math.Min(a, b);
        public static long min(long a, long b) => Math.Min(a, b);

        public static int ceilpow2(int value)
        {
            if (value <= 1)
                return 1;
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }

        public static int lzcnt(int value)
        {
            return BitOperations.LeadingZeroCount((uint)value);
        }

        public static int lzcnt(uint value)
        {
            return BitOperations.LeadingZeroCount(value);
        }
    }
}

