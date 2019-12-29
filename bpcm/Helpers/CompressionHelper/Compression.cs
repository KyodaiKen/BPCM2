using PCM.Arithmetic;
using PCM.BZip2;
using PCM.SevenZip.Compression.LZMA;
using System;
using System.Collections.Generic;
using System.IO;

namespace BPCM.CompressionHelper
{
    public struct Entropy
        {
            public double entropy;
            public ulong minsize;
        }

        public struct Compressed
        {
            public byte[] data;
            public InfoByte.CompressionType usedAlgo;
        }

        public enum Algorithm : byte
        {
            none = 0,
            BZIP2 = 1,
            lzma = 2,
            arithmetic = 3,
            fast = 4,
            bruteForce = 10
        }

        
    public static class Compression
    {
        private static byte[] InternalCompress(byte[] data, InfoByte.CompressionType algo = InfoByte.CompressionType.Arithmetic)
        {
            byte[] dataCompr = Array.Empty<byte>();

            switch (algo)
            {
                case InfoByte.CompressionType.LZMA:
                default:
                    dataCompr = SevenZipHelper.Compress(data);
                    break;

                case InfoByte.CompressionType.BZIP2:
                    using (MemoryStream msOut = new MemoryStream())
                    {
                        msOut.Write(BitConverter.GetBytes(data.Length), 0, 4);
                        using (ParallelBZip2OutputStream c = new ParallelBZip2OutputStream(msOut, 9, true))
                        {
                            c.MaxWorkers = 4;
                            c.Write(data, 0, data.Length);
                            c.Flush();
                            c.Close();
                            msOut.Flush();
                            dataCompr = msOut.ToArray();
                        }
                    }
                    break;

                case InfoByte.CompressionType.Arithmetic:
                    AbstractModel ac = new ModelOrder0();
                    //data = threeRLEencode(data);
                    using (MemoryStream mcCompr = new MemoryStream())
                    {
                        ac.Process(new MemoryStream(data), mcCompr, ModeE.MODE_ENCODE);
                        mcCompr.Flush();
                        dataCompr = mcCompr.ToArray();
                    }
                    //dataCompr = threeRLEencode(dataCompr);
                    break;
            }

            return dataCompr;
        }

        public static Compressed Compress(byte[] data, Algorithm algo = Algorithm.arithmetic)
        {
            Compressed cret = new Compressed();
            if (algo == Algorithm.bruteForce || algo == Algorithm.fast)
            {
                List<Compressed> clist = new List<Compressed>(3);
                Compressed cadd = new Compressed();
                cadd.data = InternalCompress(data, InfoByte.CompressionType.Arithmetic);
                cadd.usedAlgo = InfoByte.CompressionType.Arithmetic;
                clist.Add(cadd);

                if (algo != Algorithm.fast)
                {
                    cadd = new Compressed();
                    cadd.data = InternalCompress(data, InfoByte.CompressionType.BZIP2);
                    cadd.usedAlgo = InfoByte.CompressionType.BZIP2;
                    clist.Add(cadd);
                }

                cadd = new Compressed();
                cadd.data = InternalCompress(data, InfoByte.CompressionType.LZMA);
                cadd.usedAlgo = InfoByte.CompressionType.LZMA;
                clist.Add(cadd);

                int smallest = int.MaxValue;
                foreach (Compressed c in clist)
                {
                    if (smallest > c.data.Length)
                    {
                        smallest = c.data.Length;
                        cret = c;
                    }
                }

                clist.Clear();
                clist = null;
                return cret;
            }
            else
            {
                cret.data = InternalCompress(data, (InfoByte.CompressionType)algo);
                cret.usedAlgo = (InfoByte.CompressionType)algo;
                return cret;
            }
        }

        public static byte[] Decompress(byte[] data, InfoByte.CompressionType comprUsed)
        {
            byte[] dcd = Array.Empty<byte>();
            try
            {
                switch (comprUsed)
                {
                    case InfoByte.CompressionType.Arithmetic:
                        AbstractModel ac = new ModelOrder0();
                        using (MemoryStream msd = new MemoryStream())
                        {
                            using MemoryStream min = new MemoryStream(data);
                            min.Flush();
                            min.Position = 0;
                            ac.Process(min, msd, ModeE.MODE_DECODE);
                            msd.Flush();
                            dcd = msd.ToArray();
                        }
                        break;

                    case InfoByte.CompressionType.BZIP2:
                        using (MemoryStream msOut = new MemoryStream(data))
                        {
                            byte[] blen = new byte[4];
                            msOut.Read(blen, 0, 4);
                            int len = BitConverter.ToInt32(blen, 0);
                            using BZip2InputStream d = new BZip2InputStream(msOut);
                            dcd = new byte[len];
                            d.Read(dcd, 0, dcd.Length);
                        }
                        break;

                    case InfoByte.CompressionType.LZMA:
                        return SevenZipHelper.Decompress(data);

                    case InfoByte.CompressionType.None:
                        return data;
                }
            }
            catch { }
            return dcd;
        }
    }
}