using BPCM_CODEC;
using BPCM_CODEC.Helpers;
using NAudioLitle.Wave;
using PCM;
using System;

namespace BPCM_PLAYER
{
    class BPCMWaveProvider : IWaveProvider
    {
        private readonly WaveFormat waveFormat;
        WaveFormat IWaveProvider.WaveFormat { get { return waveFormat; } }

        public delegate void delegateReadDone(Frame CurrentFrame);

        private delegateReadDone m_evtReadDone;
        public delegateReadDone readDone { get { return m_evtReadDone; } set { m_evtReadDone = value; } }
        private float vol;
        private double srfactor;
        public float volume { get { return vol; } set { vol = value; } }

        private BitstreamReader streamBPCM;
        private RingBuffer rb;
        private int rbSize;
        private Frame frame0;
        private Frame currentFrame;
        private double tsOffset;

        public BPCMWaveProvider(BitstreamReader stream, double srf = 1)
        {
            streamBPCM = stream;
            frame0 = streamBPCM.Analysis.FrameSet[0];
            currentFrame = frame0;
            vol = 1;
            srfactor = srf;
            int speedSR = (int)Math.Round(frame0.SamplingRate * srf, 0);
            waveFormat = WaveFormat.CreateCustomFormat(WaveFormatEncoding.Pcm, speedSR, frame0.Channels, speedSR * frame0.Channels * 2, frame0.Channels * 2, 16);
            rbSize = streamBPCM.Analysis.BlockSizeMaximum * frame0.Channels * 8;
            rb = new RingBuffer(rbSize);
            tsOffset = 0;
        }

        public void DropRingBuffer()
        {
            rb = new RingBuffer(rbSize);
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            //Fill ring buffer when needed
            streamBPCM.DecodingVolume = vol;

            while (rb.Count < buffer.Length || rb.Count == 0)
            {
                object tmp = streamBPCM.GetFrame();
                if (tmp.Equals(false)) break;
                if (streamBPCM.EOF) break;
                Frame frame = (Frame)tmp;
                tmp = null;
                tsOffset = 0;
                if (!Equals(frame.Data, null))
                {
                    rb.Write(frame.Data);
                }
                else
                {
                    byte[] silence = new byte[frame.SampleCount * frame.Channels * 2];
                    for (int i = 0; i < silence.Length; i++) silence[i] = 0;

                    //Set volume info to silence
                    frame.VolumeInfo = new VolumeInfo[2];
                    frame.VolumeInfo[0].dbPeak = double.NegativeInfinity;
                    frame.VolumeInfo[1].dbPeak = double.NegativeInfinity;
                    frame.VolumeInfo[0].dbAvg = double.NegativeInfinity;
                    frame.VolumeInfo[1].dbAvg = double.NegativeInfinity;

                    rb.Write(silence);
                }
                currentFrame = frame;
            }

            if (rb.Count != 0)
            {
                int newLen = buffer.Length;
                if (newLen > rb.Count) newLen = rb.Count;
                rb.Read(newLen).CopyTo(buffer, 0);
                VolumeInfo[] vi = currentFrame.VolumeInfo;
                int cf = currentFrame.FrameNumber;
                if (cf < 0) cf = 0;
                if (cf >= streamBPCM.Analysis.FrameSet.Count) cf = streamBPCM.Analysis.FrameSet.Count - 1;
                currentFrame = streamBPCM.Analysis.FrameSet[cf];
                currentFrame.TimeStamp += tsOffset;
                tsOffset += (buffer.Length / (double)(currentFrame.Channels * 2 * currentFrame.SamplingRate));
                currentFrame.VolumeInfo = vi;
                readDone?.Invoke(currentFrame);

                return newLen;
            }
            else return 0;
        }
    }
}
