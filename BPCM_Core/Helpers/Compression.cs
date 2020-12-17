using PCM.Arithmetic;
using PCM.SevenZip.Compression.LZMA;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace BPCM.CompressionHelper
{
    public struct Compressed
    {
        public byte[] data;
        public CompressionType usedAlgo;
    }

    public static class Compression
    {
        private static byte[] InternalCompress(byte[] data, CompressionType algo = CompressionType.Arithmetic)
        {
            byte[] dataCompr = Array.Empty<byte>();

            switch (algo)
            {
                case CompressionType.LZMA:
                default:
                    dataCompr = SevenZipHelper.Compress(data);
                    break;

                case CompressionType.brotli:
                    /*using (MemoryStream msOut = new MemoryStream())
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
                    }*/

                    dataCompr = new byte[data.Length];
                    using (var be = new BrotliEncoder(11, 12))
                    {
                        int shit1, written;
                        be.Compress(new Span<byte>(data), dataCompr, out shit1, out written, true);
                        Array.Resize(ref dataCompr, written);
                    }

                    break;

                case CompressionType.Arithmetic:
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
                cadd.data = InternalCompress(data, CompressionType.Arithmetic);
                cadd.usedAlgo = CompressionType.Arithmetic;
                clist.Add(cadd);

                if (algo != Algorithm.fast)
                {
                    cadd = new Compressed();
                    cadd.data = InternalCompress(data, CompressionType.brotli);
                    cadd.usedAlgo = CompressionType.brotli;
                    clist.Add(cadd);
                }

                cadd = new Compressed();
                cadd.data = InternalCompress(data, CompressionType.LZMA);
                cadd.usedAlgo = CompressionType.LZMA;
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
                cret.data = InternalCompress(data, (CompressionType)algo);
                cret.usedAlgo = (CompressionType)algo;
                return cret;
            }
        }

        public static byte[] Decompress(byte[] data, CompressionType comprUsed)
        {
            byte[] dcd = Array.Empty<byte>();
            try
            {
                switch (comprUsed)
                {
                    case CompressionType.Arithmetic:
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

                    case CompressionType.brotli:
                        /*using (MemoryStream msOut = new MemoryStream(data))
                        {
                            byte[] blen = new byte[4];
                            msOut.Read(blen, 0, 4);
                            int len = BitConverter.ToInt32(blen, 0);
                            using BZip2InputStream d = new BZip2InputStream(msOut);
                            dcd = new byte[len];
                            d.Read(dcd, 0, dcd.Length);
                        }*/

                        var decompressedData = new Span<byte>(new byte[48006]);

                        int shit1, written;
                        System.Buffers.OperationStatus succ;
                        using (var bd = new BrotliDecoder())
                        {
                            succ = bd.Decompress(new Span<byte>(data), decompressedData, out shit1, out written);
                        }
                        var dec_data = decompressedData.ToArray();
                        decompressedData.Clear();
                        if(dec_data.Length>written) Array.Resize(ref dec_data, written);
                        return dec_data;

                    case CompressionType.LZMA:
                        return SevenZipHelper.Decompress(data);

                    case CompressionType.None:
                        return data;
                }
            }
            catch { }
            return dcd;
        }
    }
}