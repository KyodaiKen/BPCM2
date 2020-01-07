using BPCM.CompressionHelper;
using PCM;
using PCM.ADPCM;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace BPCM
{
    public class InfoByte
    {
        #region Properties

        public int SamplingRate { get; set; }
        public DataInfo DataLengthInfo { get; set; }
        public CompressionType Compression { get; set; }

        // Custom Properties
        private byte _NumberOfChannels; //It's channel count -1
        public byte NumberOfChannels { get => (byte)(_NumberOfChannels + 1); set => _NumberOfChannels = (byte)(value - 1); } //It's channel count -1

        private byte _UseLastSampleCount;
        public bool UseLastSampleCount { get => (_UseLastSampleCount == 1); set => _UseLastSampleCount = (byte)(value == true ? 1 : 0); }

        #endregion Properties

        #region I/O

        public bool SetByte(byte value)
        {
            //Read info from the byte provided by using bit shifting
            byte tmp;
            DataLengthInfo = (DataInfo)(value >> 6);
            tmp = (byte)(value << 2); Compression = (CompressionType)(tmp >> 6);
            tmp = (byte)(value << 4); _NumberOfChannels = (byte)(tmp >> 7);
            tmp = (byte)(value << 5); _UseLastSampleCount = (byte)(tmp >> 7);
            tmp = (byte)(value << 6); tmp = (byte)(tmp >> 6);

            //Convert sampling rate into usable value
            switch (tmp)
            {
                case 0:
                    SamplingRate = 44100;
                    break;

                case 1:
                    SamplingRate = 48000;
                    break;

                case 2:
                    SamplingRate = 32000;
                    break;

                case 3:
                    SamplingRate = 24000;
                    break;

                default:
                    SamplingRate = -1;
                    break;
            }
            return true;
        }

        public bool GetByte(out byte composed)
        {
            //Convert sampling rate into the table indexes
            byte sr;
            switch (SamplingRate)
            {
                case 44100:
                    sr = 0;
                    break;

                case 48000:
                    sr = 1;
                    break;

                case 32000:
                    sr = 2;
                    break;

                case 24000:
                    sr = 3;
                    break;

                default:
                    sr = 0;
                    break;
            }

            //Compose bit values into our byte
            composed = (byte)((byte)DataLengthInfo << 6);
            composed |= (byte)((byte)Compression << 4);
            composed |= (byte)(_NumberOfChannels << 3);
            composed |= (byte)(_UseLastSampleCount << 2);
            composed |= sr;
            return true;
        }

        #endregion I/O
    }

    public class BitstreamReader
    {
        //Constants
        private const byte Sync = 0xB1;

        //Instance variables
        private Stream i_Stream;

        private bool i_EOF;
        private Stats i_Stats;
        private float i_Volume;
        private int i_FramesDecoded;
        private int i_LastSampleCount;
        private bool i_Seeking; //To prevent threading nightmares...
        private bool i_EnableDither;
        private dAnalysisProgressUpdate i_AnalysisProgressUpdate;

        //Delegates
        public delegate void dAnalysisProgressUpdate(float percentDone);

        //Properties
        public float DecodingVolume { get => i_Volume; set => i_Volume = value; }

        public bool EnableDither { get => i_EnableDither; set => i_EnableDither = value; }

        //Read only
        public bool EOF { get => i_EOF; }

        public Stream BPCMStream { get => i_Stream; }
        public Stats Analysis { get => i_Stats; }
        public int FramesDecoded { get => i_FramesDecoded; }

        public BitstreamReader(Stream BPCMStream, int MaxRetries = 128, bool Analyze = true, dAnalysisProgressUpdate aupevt = null)
        {
            if (!Equals(i_Stream, null) && !i_Stream.CanRead) throw new Exception("Stream is not initialized!");
            i_Stream = BPCMStream;
            i_Volume = 1;
            if (Analyze)
            {
                i_AnalysisProgressUpdate = aupevt;
                AnalyzeStream();
                Reset();
            }
        }

        public void Reset()
        {
            Seek(0);
            i_EOF = false;
            i_FramesDecoded = 0;
            i_LastSampleCount = 0;
        }

        private object FindNextFrame()
        {
            //Some checks first
            if (Equals(i_Stream, null)) return false;
            if (i_EOF) return false;

            bool found = false;

            //Set up our info byte parser
            InfoByte ib = new InfoByte();

            bool bs__error = false;

            while (i_Stream.Position < i_Stream.Length)
            {
                if (i_Stream.ReadByte() == Sync) //We found a sync code! Let's read the info byte and do a plausability check
                    if (ib.SetByte((byte)i_Stream.ReadByte()))
                    {
                        if (bs__error)
                        {
                            Console.WriteLine("Sync found at offset {0}", i_Stream.Position);
                            bs__error = false;
                        }
                        found = true; //Found!
                    }
                if (found) break;

                if (!bs__error)
                {
                    Console.WriteLine("BITSTREAM ERROR: Garbage at offset {0}! Sync lost! Trying to resync...", i_Stream.Position);
                    bs__error = true;
                }
            }

            if (found) return ib; else return false;
        }

        private byte[] Decode(byte[] BPCM_In, CompressionType CompressionType, out VolumeInfo[] vi, bool is_mono = false, float Volume = 1)
        {
            //Decode ADPCM to raw pcm_s16le
            byte[] data = Compression.Decompress(BPCM_In, CompressionType);
            vi = new VolumeInfo[2];
            vi[0].dbPeak = double.NegativeInfinity;
            vi[1].dbPeak = double.NegativeInfinity;
            vi[0].dbAvg = double.NegativeInfinity;
            vi[1].dbAvg = double.NegativeInfinity;

            if (data.LongLength == 0)
            {
                //Decoding error, output silence
                return new byte[i_LastSampleCount * Analysis.FrameSet[0].Channels * 2];
            }

            if (is_mono)
            {
                ADPCM4BIT_MONO adpcmCoder = new ADPCM4BIT_MONO();
                //VolumeInfo must be converted to array. Not ideal...
                VolumeInfo vim;
                vi = new VolumeInfo[2];
                var decoded = adpcmCoder.decode(data, out vim, true, Volume);
                vi[0] = vim;
                vi[1] = vim;
                return decoded;
            }
            else
            {
                ADPCM4BIT adpcmCoder = new ADPCM4BIT();
                return adpcmCoder.decode(data, out vi, false, true, i_EnableDither, Volume);
            }
        }

        public object GetFrame(bool DecodeFrame = true)
        {
            //Make sure we are pointing to a frame after its sync and info byte!
            object Found = FindNextFrame();
            if (Equals(Found, false)) return false;

            //Convert our object into an InfoByte, cause it is one.
            InfoByte ib = (InfoByte)Found; Found = null;

            //Create a new frame object
            Frame OurFrame = new Frame();

            //Universal byte array!
            byte[] tmp;

            //If this frame is silent but no compression is set, then this is an invalid condition!
            if (ib.DataLengthInfo == DataInfo.Silent && ib.Compression == 0) return false;

            //Frame has no sample count but we didn't got one yet, so we have to force decoding to get it!
            bool DecodeAnyways = (ib.UseLastSampleCount && i_LastSampleCount == 0 && ib.DataLengthInfo != DataInfo.Silent);

            //Init header size
            OurFrame.HederLength = 2;

            //Setting some basic info
            OurFrame.Channels = ib.NumberOfChannels;
            OurFrame.SamplingRate = ib.SamplingRate;
            OurFrame.DataInfo = ib.DataLengthInfo;

            //Get sample count from frame header if given
            if (ib.DataLengthInfo != DataInfo.Silent)
            {
                if (!ib.UseLastSampleCount)
                {
                    tmp = new byte[2];
                    i_Stream.Read(tmp, 0, 2);
                    OurFrame.SampleCount = BitConverter.ToUInt16(tmp, 0);
                    tmp = null;

                    //Don't forget to increase header size
                    OurFrame.HederLength += 2;

                    //Remember sample count in case the next frame(s) doesn't have this value given in the header!
                    i_LastSampleCount = OurFrame.SampleCount;
                }
                else
                {
                    if (i_LastSampleCount > 0) OurFrame.SampleCount = i_LastSampleCount;
                }
            }

            //If this frame is silent but a compression is set, get the number of samples instead to determine the length
            if (ib.DataLengthInfo == DataInfo.Silent && ib.Compression > 0)
            {
                OurFrame.DataLength = 0;
                switch (ib.Compression)
                {
                    case CompressionType.brotli:
                        OurFrame.SampleCount = i_Stream.ReadByte();
                        OurFrame.HederLength += 1;
                        break;

                    case CompressionType.LZMA:
                        tmp = new byte[2];
                        i_Stream.Read(tmp, 0, 2);
                        OurFrame.SampleCount = BitConverter.ToUInt16(tmp, 0);
                        tmp = null;
                        OurFrame.HederLength += 2;
                        break;

                    case CompressionType.Arithmetic:
                        tmp = new byte[4];
                        i_Stream.Read(tmp, 0, 3);
                        OurFrame.SampleCount = (int)BitConverter.ToUInt32(tmp, 0);
                        tmp = null;
                        OurFrame.HederLength += 3;
                        break;
                }

                //Remember sample count in case the next frame(s) doesn't have this value given in the header!
                i_LastSampleCount = OurFrame.SampleCount;

                //OMG we're lying!!!
                OurFrame.CompressionType = CompressionType.None;
            }
            else
            {
                //Process the next bytes as data length normally
                switch (ib.DataLengthInfo)
                {
                    case DataInfo.LengthAddressingByte:
                        OurFrame.DataLength = (byte)i_Stream.ReadByte();
                        OurFrame.HederLength += 1;

                        break;

                    case DataInfo.LengthAddressingShort:
                        tmp = new byte[2];
                        i_Stream.Read(tmp, 0, 2);
                        OurFrame.DataLength = BitConverter.ToUInt16(tmp, 0);
                        tmp = null;
                        OurFrame.HederLength += 2;
                        break;

                    case DataInfo.LengthAddressing24Bit:
                        tmp = new byte[4];
                        i_Stream.Read(tmp, 0, 3);
                        OurFrame.DataLength = BitConverter.ToUInt32(tmp, 0);
                        tmp = null;
                        OurFrame.HederLength += 3;
                        break;

                    case DataInfo.Silent:
                        OurFrame.DataLength = 0;
                        break;
                }

                //Here we are honest!
                OurFrame.CompressionType = ib.Compression;
            }

            //Set data position offset
            OurFrame.DataOffset = i_Stream.Position - OurFrame.HederLength;

            //Volume info
            VolumeInfo[] vi;

            //In case we need to decode the actual frame
            if ((DecodeFrame || DecodeAnyways) && ib.DataLengthInfo != DataInfo.Silent)
            {
                //Before getting any data, first check if the stream ended
                if (i_Stream.Position + OurFrame.DataLength > i_Stream.Length)
                {
                    i_EOF = true;
                    return false;
                }
                else if (i_Stream.Position + OurFrame.DataLength == i_Stream.Length)
                    i_EOF = true;

                //Get the actual compressed (or not compressed) data
                tmp = new byte[OurFrame.DataLength];
                i_Stream.Read(tmp, 0, (int)OurFrame.DataLength);

                //Decompress
                OurFrame.Data = Decode(tmp, OurFrame.CompressionType, out vi, OurFrame.Channels == 1, i_Volume);
                tmp = null;

                //Store volume info
                OurFrame.VolumeInfo = vi;

                if (!DecodeFrame) OurFrame.Data = null;

                //Calculate duration and sample count from actual decoded data
                OurFrame.SampleCount = (int)(OurFrame.Data.LongLength / OurFrame.Channels / 2);
                OurFrame.Duration = OurFrame.SampleCount / (double)OurFrame.SamplingRate;

                //Remember sample count in case the next frame(s) doesn't have this value given in the header!
                i_LastSampleCount = OurFrame.SampleCount;
            }

            if (!DecodeFrame && !DecodeAnyways && ib.DataLengthInfo != DataInfo.Silent)
                i_Stream.Seek(OurFrame.DataLength, SeekOrigin.Current);

            OurFrame.Duration = OurFrame.SampleCount / (double)OurFrame.SamplingRate;

            OurFrame.FrameNumber = i_FramesDecoded;

            i_FramesDecoded++;

            //Wrap it up
            if (i_Seeking) i_Seeking = false;
            return OurFrame;
        }

        public bool AnalyzeStream()
        {
            //Set initial stat values
            i_Stats = new Stats();
            i_Stats.BitrateMinimum = int.MaxValue;
            i_Stats.BitrateAverage = 0;
            i_Stats.BitrateMaximum = 0;
            i_Stats.BlockSizeMinimum = int.MaxValue;
            i_Stats.BlockSizeAverage = 0;
            i_Stats.BlockSizeMaximum = 0;
            i_Stats.BlockSizeNominal = 0;
            i_Stats.LongestSilentFrame = 0;

            //Local vars to be updated by the loop
            long sum_bpcm_bytes = 0;
            long sum_samples = 0;
            long LastFrameDuration = 0;
            Dictionary<int, long> FrmeSmplCtHistNonSlnt = new Dictionary<int, long>();
            Dictionary<int, long> FrmeSmplCtHist = new Dictionary<int, long>();

            //Frameset
            List<Frame> frames = new List<Frame>();

            //Collection of used compression algorithms
            List<string> ctypes = new List<string>();

            Stopwatch sw = new Stopwatch();
            sw.Start();

            //Loop until end of stream
            while (!i_EOF)
            {
                object tmp = GetFrame(false);
                if (tmp.Equals(false)) break;
                Frame frame = (Frame)tmp;
                tmp = null;

                //Set calculated total duration BEFORE we add the length of this frame
                frame.TimeStamp = sum_samples / (double)frame.SamplingRate;

                //Add frame number
                frame.FrameNumber = frames.Count;

                //Collect possibly new compression type
                string ctype_descr = frame.CompressionType.ToString().ToLower();
                if (frame.CompressionType == CompressionType.None && frame.DataInfo == DataInfo.Silent) ctype_descr = "silence";
                if (!ctypes.Contains(ctype_descr)) ctypes.Add(ctype_descr);

                //Determine compression description
                frame.CompressionTypeDescr = ctype_descr;

                //Add frame to frameset
                frames.Add(frame);

                //Update stats
                double bitrate = ((frame.DataLength + frame.HederLength) / frame.Duration) * 8;
                if (bitrate > i_Stats.BitrateMaximum) i_Stats.BitrateMaximum = (int)Math.Round(bitrate, 0);
                if (bitrate < i_Stats.BitrateMinimum) i_Stats.BitrateMinimum = (int)Math.Round(bitrate, 0);
                if (frame.SampleCount > i_Stats.BlockSizeMaximum) i_Stats.BlockSizeMaximum = frame.SampleCount;
                if (frame.SampleCount < i_Stats.BlockSizeMinimum) i_Stats.BlockSizeMinimum = frame.SampleCount;
                sum_bpcm_bytes += frame.DataLength + frame.HederLength;
                sum_samples += frame.SampleCount;

                int currframe = frames.Count;
                if (frame.DataInfo == DataInfo.Silent && frame.SampleCount > LastFrameDuration)
                {
                    i_Stats.LongestSilentFrame = frame.SampleCount;
                    LastFrameDuration = frame.SampleCount;
                }
                //Histogram for non silent frames
                if (frame.DataInfo != DataInfo.Silent)
                {
                    bool success = FrmeSmplCtHistNonSlnt.TryGetValue(frame.SampleCount, out long val);
                    if (success) FrmeSmplCtHistNonSlnt[frame.SampleCount] = val + 1;
                    else FrmeSmplCtHistNonSlnt.Add(frame.SampleCount, 1);
                }
                //Histogram for all frames
                bool success1 = FrmeSmplCtHist.TryGetValue(frame.SampleCount, out long val1);
                if (success1) FrmeSmplCtHist[frame.SampleCount] = val1 + 1;
                else FrmeSmplCtHist.Add(frame.SampleCount, 1);

                if (sw.Elapsed.TotalMilliseconds >= (double)100) //10 FPS
                {
                    i_AnalysisProgressUpdate?.Invoke((float)i_Stream.Position / i_Stream.Length * 100f);
                    sw.Restart();
                }
            }

            sw.Stop();
            sw = null;
            //Calculate average bitrate and duration as well as used compression algorithms
            i_Stats.BitrateAverage = (int)Math.Round(sum_bpcm_bytes / (sum_samples / (double)frames[0].SamplingRate) * 8, 0);
            i_Stats.BlockSizeAverage = (int)Math.Round((double)sum_samples / frames.Count, 0);
            i_Stats.Duration = sum_samples / (double)frames[0].SamplingRate;
            i_Stats.DurationSampleCount = sum_samples;
            i_Stats.CompressionUsed = ctypes;
            i_Stats.FrameSampleCountHistogram = FrmeSmplCtHist;
            long ct = 0;
            foreach (KeyValuePair<int, long> kv in FrmeSmplCtHistNonSlnt)
            {
                if (kv.Value > ct)
                {
                    ct = kv.Value;
                    i_Stats.BlockSizeNominal = kv.Key;
                }
            }

            //Set frameset :D
            i_Stats.FrameSet = frames;
            return true;
        }

        public bool Seek(int Frame)
        {
            if (i_Seeking) return false;
            if (i_Stats.FrameSet.Equals(null)) return false;
            if (Frame < 0) Frame = 0; //Clamping
            if (Frame >= i_Stats.FrameSet.Count) Frame = i_Stats.FrameSet.Count - 1; //Clamping
            if (Frame == 0) i_FramesDecoded = 0; else i_FramesDecoded = Frame;
            i_Seeking = true;
            i_Stream.Seek(i_Stats.FrameSet[Frame].DataOffset, SeekOrigin.Begin);
            i_EOF = false;
            return true;
        }

        public bool Seek(double TimeStamp)
        {
            if (i_Stats.FrameSet.Equals(null)) return false;
            //Determine the nearest frame of the given timestamp
            for (int i = 0; i < i_Stats.FrameSet.Count; i++) if (i_Stats.FrameSet[i].TimeStamp >= TimeStamp) return Seek(i);
            return false;
        }
    }

    public static class Composer
    {
        //Constants
        private const byte Sync = 0xB1;

        public static byte[] ComposeFrame(byte[] CompressedBPCMData, int SamplingRate, int NumberOfChannels, CompressionType Compression, bool WriteSampleCount = true, UInt32 NumberOfSamples = 0)
        {
            //Prepare info byte
            InfoByte ib = new InfoByte();

            ib.Compression = Compression;
            ib.NumberOfChannels = (byte)NumberOfChannels;
            ib.SamplingRate = SamplingRate;

            //Check data size and compose it
            if (!object.Equals(CompressedBPCMData, null))
            {
                if (CompressedBPCMData.Length <= byte.MaxValue)
                    ib.DataLengthInfo = DataInfo.LengthAddressingByte;
                else if (CompressedBPCMData.Length > byte.MaxValue && CompressedBPCMData.Length <= ushort.MaxValue)
                    ib.DataLengthInfo = DataInfo.LengthAddressingShort;
                else if (CompressedBPCMData.Length > ushort.MaxValue)
                    ib.DataLengthInfo = DataInfo.LengthAddressing24Bit;
            }
            else
            {
                ib.DataLengthInfo = DataInfo.Silent;
            }

            //Header length reminder
            int HeaderLen = 2;

            ib.UseLastSampleCount = !WriteSampleCount;

            //In case we have audio data
            byte[] FrameBinary;
            if (ib.DataLengthInfo != DataInfo.Silent && !Equals(CompressedBPCMData, null))
            {
                //Init byte array for output
                FrameBinary = new byte[CompressedBPCMData.Length + HeaderLen + (int)ib.DataLengthInfo];

                //If needed, we add the sample count into the header
                if (WriteSampleCount)
                {
                    HeaderLen += 2;

                    //Re-init byte array for output
                    FrameBinary = new byte[CompressedBPCMData.Length + HeaderLen + (int)ib.DataLengthInfo];

                    //Get binary data from the sample count
                    byte[] SCBinary = BitConverter.GetBytes(NumberOfSamples);

                    //And copy it into the array
                    SCBinary.CopyTo(FrameBinary, 2);
                    SCBinary = null;
                }

                //Add sync code and info byte
                FrameBinary[0] = Sync;
                ib.GetByte(out FrameBinary[1]);

                //Get binary data from the length
                byte[] LengthBinary = BitConverter.GetBytes((uint)CompressedBPCMData.Length);

                //And copy it into the array, but just the used bytes!
                Array.Resize(ref LengthBinary, (int)ib.DataLengthInfo);
                LengthBinary.CopyTo(FrameBinary, HeaderLen);
                LengthBinary = null;

                //Put data into the array
                CompressedBPCMData.CopyTo(FrameBinary, HeaderLen + (int)ib.DataLengthInfo);
            }
            else
            {
                //Figure out the sample number data type size, we use the compression type instead
                if (NumberOfSamples <= byte.MaxValue)
                    ib.Compression = CompressionType.brotli;
                else if (NumberOfSamples > byte.MaxValue && NumberOfSamples <= ushort.MaxValue)
                    ib.Compression = CompressionType.LZMA;
                else if (NumberOfSamples > ushort.MaxValue)
                    ib.Compression = CompressionType.Arithmetic;

                //Init byte array for output
                FrameBinary = new byte[HeaderLen + (int)ib.Compression];

                FrameBinary[0] = Sync;
                ib.GetByte(out FrameBinary[1]);

                //Get binary data from the sample count
                byte[] NumberOfSamplesBinary = BitConverter.GetBytes(NumberOfSamples);

                //And copy it into the array, but just the used bytes!
                Array.Resize(ref NumberOfSamplesBinary, (int)ib.Compression);
                NumberOfSamplesBinary.CopyTo(FrameBinary, HeaderLen);
                NumberOfSamplesBinary = null;
            }

            return FrameBinary;
        }
    }
}