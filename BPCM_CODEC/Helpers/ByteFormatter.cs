using System;

namespace BPCM_CODEC.Helpers
{
    public static class ByteFormatter
    {
        static public string FormatBytes(long bytes)
        {
            const int scale = 1024;
            string[] orders = new string[] { "EByte", "PByte", "TByte", "GByte", "MByte", "KByte", "Bytes" };
            long max = (long)Math.Pow(scale, orders.Length - 1);

            foreach (string order in orders)
            {
                if (bytes > max)
                    return string.Format("{0:##.##} {1}", decimal.Divide(bytes, max), order);

                max /= scale;
            }
            return "0 Bytes";
        }
    }
}