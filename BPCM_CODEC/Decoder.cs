using PCM.PCMContainers.RIFFWave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace BPCM_CODEC
{

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