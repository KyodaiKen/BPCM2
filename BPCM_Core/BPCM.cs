using BPCM.CompressionHelper;
using PCM.ADPCM;
using PCM.PCMContainers.RIFFWave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BPCM.Easy
{
    public static class Encoder
    {
        public struct Status
        {
            public int FramesEncoded;
            public int AvgBitrate;
            public double Position;
            public long PositionSamples;
            public string PositionString;
            public double Duration;
            public long DurationSamples;
            public long BytesWritten;
        }

        public struct Stats
        {
            public int FrameNumber;
            public double TimeStamp;
            public double FrameDuration;
            public int Bytes;
            public int Bitrate;
            public Algorithm Compression;
        }

        public struct Parameters
        {
            public int BlockSize;
            public Algorithm Compression;
            public short SilenceThreshold;
        }

        public delegate void dgUpdate(Status status);

        public delegate void dgFrameEncoded(Stats stats);

        public static void EncodeWaveFile(string WaveIn, string BPCMOut, Parameters Parameters, dgUpdate StatusCallback = null, double UpdateInterval = 250, dgFrameEncoded FrameEncodedCallback = null)
        {
            using (FileStream streamIn = new FileStream(WaveIn, FileMode.Open, FileAccess.Read, FileShare.Read, 1048576, FileOptions.RandomAccess))
            {
                WAVEReader w = new WAVEReader(streamIn);

                //Check parameters and if in doubt, set default ones
                if (Parameters.BlockSize < 10) Parameters.BlockSize = 10;
                if (Parameters.BlockSize > 1000) Parameters.BlockSize = 1000;
                //Check if wFormatTag is 1 (PCM signed integer) and if not, crash!
                if (w.Info.fmtHeader.wFormatTag != 1) throw new Exception("BPCM 2.x only supports PCM integer input (wFormatTag = 0x1)");
                //Check if bits per sample is 16, if not crash!
                if (w.Info.fmtHeader.nBitsPerSample != 16) throw new Exception("BPCM 2.x only supports 16 bit signed short integer input");
                //Check if sampling rate is supported
                if (!new uint[] { 48000, 44100, 32000, 24000 }.Contains(w.Info.fmtHeader.nSamplesPerSec)) throw new Exception("BPCM 2.x only supports the sampling rates 48000, 44100, 32000 and 24000 Hz.");
                //Check number of channels to be either mono or stereo
                if (!new ushort[] { 1, 2 }.Contains(w.Info.fmtHeader.nChannels)) throw new Exception("BPCM 2.x only supports mono or stereo source wave files.");

                if (Parameters.SilenceThreshold == 0) Parameters.SilenceThreshold = 4;

                //Create BPCM output file stream
                using (FileStream f = new FileStream(BPCMOut, FileMode.Create, FileAccess.Write, FileShare.Read, 1048576, FileOptions.SequentialScan))
                {
                    //Data for the frame builder to use later
                    byte[] data = null;
                    byte compression = (byte)Parameters.Compression;

                    //Init ADPCM codec
                    ADPCM4BIT a = new ADPCM4BIT();
                    ADPCM4BIT_MONO am = new ADPCM4BIT_MONO();

                    //Stopwatch for the status callback
                    Stopwatch sw = new Stopwatch();

                    int currentFrame = 0;
                    long samplets = 0;
                    int silentSamples = 0;
                    double precisepos = 0;
                    TimeSpan pos;

                    sw.Start();
                    while (w.Position < streamIn.Length)
                    {
                        //Read the PCM data from the WAVE file, encode it with ADPCM and then wether requested compress it!
                        w.Read(out byte[] pcmBuffer, (uint)Parameters.BlockSize);

                        bool afterSilence = false;
                        byte[] compressedADPCM;
                        byte[] frame = new byte[0];
                        int nSamplesFromPCMBuffer;
                        int nSamplesSilent = 0;
                        int nSamplesFromMilliSeconds = (int)((Parameters.BlockSize / 1000.0) * w.Info.fmtHeader.nSamplesPerSec);

                        Compressed c = new Compressed();

                        Tunings.SilenceInfoBean silence_inf = Tunings.GetSilenceInfo(pcmBuffer, Parameters.SilenceThreshold);

                        switch (silence_inf.is_silent)
                        {
                            case Tunings.Silence.TotalSilence:
                                silentSamples += pcmBuffer.Length / ((w.Info.fmtHeader.nBitsPerSample / 8) * w.Info.fmtHeader.nChannels);
                                nSamplesSilent = silentSamples;
                                nSamplesFromPCMBuffer = silentSamples;
                                goto skip_write;
                            case Tunings.Silence.PartiallySilent:
                            case Tunings.Silence.FullSignal:
                                if (silence_inf.is_silent == Tunings.Silence.PartiallySilent
                                    && (silence_inf.silence_from == Tunings.SilenceAt.Beginning
                                        || silentSamples + silence_inf.buffer_pos / ((w.Info.fmtHeader.nBitsPerSample / 8) * w.Info.fmtHeader.nChannels) >= 16777215)
                                    && silentSamples > 0)
                                {
                                    //We just have silence at the beginning of the frame, but pathway through there is signal
                                    //Output a silent frame with extended length information
                                    frame = Composer.ComposeFrame(null, (int)w.Info.fmtHeader.nSamplesPerSec, w.Info.fmtHeader.nChannels, CompressionType.None, true, (UInt32)silentSamples);
                                    f.Write(frame, 0, frame.Length);
                                    if (silentSamples != nSamplesFromMilliSeconds) afterSilence = true; else afterSilence = false;
                                    nSamplesSilent = silentSamples;
                                    silentSamples = 0;
                                }
                                else afterSilence = false;

                                //Prepare regular data
                                if (w.Info.fmtHeader.nChannels == 2)
                                {
                                    compressedADPCM = a.encode(pcmBuffer, false);
                                }
                                else
                                {
                                    compressedADPCM = am.encode(pcmBuffer);
                                }

                                c = Compression.Compress(compressedADPCM, Parameters.Compression);
                                data = c.data;
                                compression = (byte)c.usedAlgo;
                                break;
                        }

                        //Recalculate the blocksize when it differs from the setting, in example when the stream ends.
                        nSamplesFromPCMBuffer = (int)(pcmBuffer.LongLength / w.Info.fmtHeader.nChannels / (w.Info.fmtHeader.nBitsPerSample / 8));
                        nSamplesFromMilliSeconds = (int)((Parameters.BlockSize / 1000.0) * w.Info.fmtHeader.nSamplesPerSec);

                        frame = Composer.ComposeFrame(data, (int)w.Info.fmtHeader.nSamplesPerSec, w.Info.fmtHeader.nChannels, (CompressionType)compression, currentFrame == 0 || nSamplesFromPCMBuffer != nSamplesFromMilliSeconds || afterSilence, (UInt16)nSamplesFromPCMBuffer);
                        f.Write(frame, 0, frame.Length);

                    skip_write:
                        currentFrame++;
                        samplets = w.PCMPosition / w.Info.fmtHeader.nBlockAlign;
                        precisepos = (double)samplets / w.Info.fmtHeader.nSamplesPerSec;
                        pos = TimeSpan.FromSeconds(precisepos);

                        //Fire the frame encoded event
                        FrameEncodedCallback?.Invoke(new Stats()
                        {
                            FrameNumber = currentFrame
                          , TimeStamp = samplets
                          , FrameDuration = (nSamplesSilent > 0) ? nSamplesSilent / (double)w.Info.fmtHeader.nSamplesPerSec : nSamplesFromPCMBuffer / (double)w.Info.fmtHeader.nSamplesPerSec
                          , Bytes = frame.Length
                          , Bitrate = (int)Math.Round((double)frame.Length / (pcmBuffer.Length / w.Info.fmtHeader.nAvgBytesPerSeconds) * 8, 0)
                          , Compression = (Algorithm)compression
                        });

                        frame = null;

                        //Do the status callback after the interval has elapsed and reset the stopwatch
                        if (sw.ElapsedMilliseconds >= UpdateInterval || w.Position >= streamIn.Length)
                        {
                            StatusCallback?.Invoke(new Status()
                            {
                                FramesEncoded = currentFrame
                              , AvgBitrate = (int)Math.Round((f.Length / precisepos) * 8.0, 0)
                              , Position = precisepos
                              , PositionSamples = samplets
                              , PositionString = String.Format("{0:00}d {1:00}h {2:00}m {3:00}s {4:000.000}ms", pos.Days, pos.Hours, pos.Minutes, pos.Seconds, (precisepos - Math.Floor(precisepos)) * 1000)
                              , BytesWritten = f.Length
                              , Duration = w.Info.PCMDataLength / w.Info.fmtHeader.nAvgBytesPerSeconds
                              , DurationSamples = w.Info.PCMDataLength / w.Info.fmtHeader.nBlockAlign
                            });
                            sw.Restart();
                        }
                    }

                    //Check if silence was collected first and flush this as a silent frame before composing the current frame
                    if (silentSamples > 0)
                    {
                        byte[] frame = Composer.ComposeFrame(null, (int)w.Info.fmtHeader.nSamplesPerSec, w.Info.fmtHeader.nChannels, CompressionType.None, true, (UInt32)silentSamples);
                        f.Write(frame, 0, frame.Length);
                    }
                }
            }
        }
    }

    public static class Decoder
    {
        public struct ConfigurationBean
        {
            public bool EnableDither { get; set; }
            public bool Analyze { get; set; }
            public double UpdateInterval { get; set; }
            public dgBPCMFileOpened FileOpenedEvent { get; set; }
            public dgUpdate ProgressUpdateEvent { get; set; }
            public dgAnalysisUpdate AnalysisProgressUpdateEvent { get; set; }
        }
        public struct Status
        {
            public double Position;
            public string PositionString;
            public long BytesWritten;
            public double PercentageDone;
        }

        public struct Info
        {
            public int NumberOfChannels;
            public int SamplingRate;
            public double Duration;
            public long DurationSampleCount;
            public string DurationString;
            public int BitrateMin;
            public int BitrateAvg;
            public int BitrateMax;
            public int BlockSizeMinimum;
            public int BlockSizeAverage;
            public int BlockSizeMaximum;
            public int BlockSizeNominal;

            public Dictionary<int, long> FrameSampleCountHistogram;
            public Dictionary<string, long> FrameCompressionHistogram;

            public List<Frame> FrameSet;
            public List<string> CompressionUsed;
            public string CompressionUsedString;
            //public long DataBytes;
            //public long BitstreamBytes;
            public long FileSize;
        }

        public delegate void dgBPCMFileOpened(Info Info);

        public delegate void dgUpdate(Status Status);

        public delegate void dgAnalysisUpdate(float progress);

        public static Info AnalyzeFile(string bpcmFile, dgAnalysisUpdate progressUpdate = null)
        {
            using (FileStream s = new FileStream(bpcmFile, FileMode.Open, FileAccess.Read, FileShare.Read, 1048576, FileOptions.RandomAccess))
            {
                //Initialize BPCM stream reader with or without delegation for the analysis progress update
                BitstreamReader BPCM;
                if (progressUpdate != null)
                {
                    void updateFunc(float progress)
                    {
                        progressUpdate.Invoke(progress);
                    }
                    BPCM = new BitstreamReader(s, aupevt: updateFunc);
                }
                else
                {
                    BPCM = new BitstreamReader(s);
                }

                double duration = (double)BPCM.Analysis.DurationSampleCount / BPCM.Analysis.FrameSet[0].SamplingRate;
                TimeSpan dur = TimeSpan.FromSeconds(duration);
                string strDuration = String.Format("{0:00}d {1:00}h {2:00}m {3:00}s {4:000.000}ms", dur.Days, dur.Hours, dur.Minutes, dur.Seconds, (duration - Math.Floor(duration)) * 1000);
                return new Info()
                {
                      NumberOfChannels = BPCM.Analysis.FrameSet[0].Channels
                    , SamplingRate = BPCM.Analysis.FrameSet[0].SamplingRate
                    , Duration = BPCM.Analysis.Duration
                    , DurationSampleCount = BPCM.Analysis.DurationSampleCount
                    , DurationString = strDuration
                    , BitrateMin = BPCM.Analysis.BitrateMinimum
                    , BitrateAvg = BPCM.Analysis.BitrateAverage
                    , BitrateMax = BPCM.Analysis.BitrateMaximum
                    , BlockSizeNominal = BPCM.Analysis.BlockSizeNominal
                    , BlockSizeAverage = BPCM.Analysis.BlockSizeAverage
                    , BlockSizeMaximum = BPCM.Analysis.BlockSizeMaximum
                    , BlockSizeMinimum = BPCM.Analysis.BlockSizeMinimum
                    , FrameSampleCountHistogram = BPCM.Analysis.FrameSampleCountHistogram
                    , FrameCompressionHistogram = BPCM.Analysis.CompressionHistogram
                    , FrameSet = BPCM.Analysis.FrameSet
                    , CompressionUsed = BPCM.Analysis.CompressionUsed
                    , CompressionUsedString = string.Join(", ", BPCM.Analysis.CompressionUsed.ToArray())
                };
            }
        }

        public static void DecodeBPCMFile(string bpcmFile, string waveFile, ConfigurationBean config)
        {
            using (FileStream s = new FileStream(bpcmFile, FileMode.Open, FileAccess.Read, FileShare.Read, 1048576, FileOptions.RandomAccess))
            {
                BitstreamReader BPCM;
                if (config.AnalysisProgressUpdateEvent is null)
                {
                    BPCM = new BitstreamReader(s);
                }
                else
                {
                    void updateFunc(float progress)
                    {
                        config.AnalysisProgressUpdateEvent.Invoke(progress);
                    }
                    BPCM = new BitstreamReader(s, aupevt: updateFunc);
                }

                double duration = (double)BPCM.Analysis.DurationSampleCount / BPCM.Analysis.FrameSet[0].SamplingRate;
                TimeSpan dur = TimeSpan.FromSeconds(duration);
                string strDuration = string.Format("{0:00}d {1:00}h {2:00}m {3:00}s {4:000.000}ms", dur.Days, dur.Hours, dur.Minutes, dur.Seconds, (duration - Math.Floor(duration)) * 1000);
                config.FileOpenedEvent?.Invoke(new Info()
                {
                      NumberOfChannels = BPCM.Analysis.FrameSet[0].Channels
                    , SamplingRate = BPCM.Analysis.FrameSet[0].SamplingRate
                    , Duration = BPCM.Analysis.Duration
                    , DurationSampleCount = BPCM.Analysis.DurationSampleCount
                    , DurationString = strDuration
                    , BitrateMin = BPCM.Analysis.BitrateMinimum
                    , BitrateAvg = BPCM.Analysis.BitrateAverage
                    , BitrateMax = BPCM.Analysis.BitrateMaximum
                    , BlockSizeNominal = BPCM.Analysis.BlockSizeNominal
                    , BlockSizeAverage = BPCM.Analysis.BlockSizeAverage
                    , BlockSizeMaximum = BPCM.Analysis.BlockSizeMaximum
                    , BlockSizeMinimum = BPCM.Analysis.BlockSizeMinimum
                    , FrameSampleCountHistogram = BPCM.Analysis.FrameSampleCountHistogram
                    , FrameCompressionHistogram = BPCM.Analysis.CompressionHistogram
                    , FrameSet = BPCM.Analysis.FrameSet
                    , CompressionUsed = BPCM.Analysis.CompressionUsed
                    , CompressionUsedString = string.Join(", ", BPCM.Analysis.CompressionUsed.ToArray())
                    , FileSize = BPCM.BPCMStream.Length
                });

                BPCM.EnableDither = config.EnableDither;

                using (FileStream streamOut = new FileStream(waveFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 1048576, FileOptions.RandomAccess))
                {
                    WAVEWriter w = new WAVEWriter(streamOut, new WAVEFormat()
                    {
                          nSamplesPerSec = (uint)BPCM.Analysis.FrameSet[0].SamplingRate
                        , nChannels = (ushort)BPCM.Analysis.FrameSet[0].Channels
                        , nBitsPerSample = 16
                        , nBlockAlign = (ushort)(BPCM.Analysis.FrameSet[0].Channels * 2)
                        , nAvgBytesPerSeconds = (uint)(BPCM.Analysis.FrameSet[0].Channels * 2 * BPCM.Analysis.FrameSet[0].SamplingRate)
                        , wFormatTag = 1 //PCM signed integer Intel
                    });

                    Stopwatch ssw = new Stopwatch();
                    ssw.Start();

                    double precisepos;
                    TimeSpan pos;

                    while (true)
                    {
                        object tmp = BPCM.GetFrame();
                        if (tmp.Equals(false)) break;
                        Frame frame = (Frame)tmp;
                        tmp = null;

                        if (frame.Data?.Length > 0)
                            w.Write(frame.Data);
                        else
                            w.WriteSilence((double)frame.SampleCount / frame.SamplingRate);

                        if (ssw.Elapsed.TotalMilliseconds >= config.UpdateInterval && !config.ProgressUpdateEvent.Equals(null))
                        {
                            precisepos = (double)w.PCMPosition / w.fmtHeader.nAvgBytesPerSeconds;
                            pos = TimeSpan.FromSeconds(precisepos);
                            config.ProgressUpdateEvent.Invoke(new Status()
                            {
                                  BytesWritten = w.PCMPosition
                                , Position = (double)w.PCMPosition / w.fmtHeader.nAvgBytesPerSeconds
                                , PositionString = String.Format("{0:00}d {1:00}h {2:00}m {3:00}s {4:000.000}ms", pos.Days, pos.Hours, pos.Minutes, pos.Seconds, (precisepos - Math.Floor(precisepos)) * 1000)
                                , PercentageDone = (double)s.Position / s.Length * 100
                            });
                            ssw.Restart();
                        }
                        if (BPCM.EOF) break;
                    }

                    precisepos = (double)w.PCMPosition / w.fmtHeader.nAvgBytesPerSeconds;
                    pos = TimeSpan.FromSeconds(precisepos);

                    config.ProgressUpdateEvent?.Invoke(new Status()
                    {
                          BytesWritten = w.PCMPosition
                        , Position = (double)w.PCMPosition / w.fmtHeader.nAvgBytesPerSeconds
                        , PositionString = String.Format("{0:00}d {1:00}h {2:00}m {3:00}s {4:000.000}ms", pos.Days, pos.Hours, pos.Minutes, pos.Seconds, (precisepos - Math.Floor(precisepos)) * 1000)
                        , PercentageDone = 100
                    });
                    w.Finalize();
                }
            }
        }
    }
}