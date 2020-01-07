using PCM;
using System.Collections.Generic;

namespace BPCM
{
    #region Enums
    public enum DataInfo : byte
    {
        Silent = 0,
        LengthAddressingByte = 1,
        LengthAddressingShort = 2,
        LengthAddressing24Bit = 3
    }

    public enum CompressionType : byte
    {
        None = 0,
        brotli = 1,
        LZMA = 2,
        Arithmetic = 3
    }

    public enum Algorithm : byte
    {
        none = 0,
        brotli = 1,
        lzma = 2,
        arithmetic = 3,
        fast = 4,
        bruteForce = 10
    }
    #endregion Enums
    #region Structures
    public struct Frame
    {
        public int FrameNumber;
        public DataInfo DataInfo;
        public int HederLength;
        public CompressionType CompressionType;
        public string CompressionTypeDescr;
        public int Channels;
        public int SamplingRate;
        public double TimeStamp;
        public long DataOffset;
        public int SampleCount;
        public double Duration;
        public bool UseLastSampleCount;
        public long DataLength;
        public byte[] Data;
        public VolumeInfo[] VolumeInfo;
    }

    public struct Stats
    {
        public int BitrateMinimum;
        public int BitrateAverage;
        public int BitrateMaximum;
        public int BlockSizeMinimum;
        public int BlockSizeAverage;
        public int BlockSizeMaximum;
        public int BlockSizeNominal;
        public int LongestSilentFrame;
        public double Duration;
        public long DurationSampleCount;
        public List<Frame> FrameSet;
        public List<string> CompressionUsed;
        public Dictionary<int, long> FrameSampleCountHistogram;
    }
    #endregion Structures
}
