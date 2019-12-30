using PCM.ADPCM;
using System;
using BPCM.Helpers;
using NAudioLitle.Wave;

namespace BPCM
{
    public class BPCMWaveProvider : IWaveProvider
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
            rb = new RingBuffer(2097152);
            tsOffset = 0;
        }

        public void DropRingBuffer()
        {
            rb = new RingBuffer(2097152);
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
                    frame.VolumeInfo = new ADPCM4BIT.VolumeInfo();
                    frame.VolumeInfo.dbPeakL = double.NegativeInfinity;
                    frame.VolumeInfo.dbPeakR = double.NegativeInfinity;
                    frame.VolumeInfo.dbAvgL = double.NegativeInfinity;
                    frame.VolumeInfo.dbAvgR = double.NegativeInfinity;

                    rb.Write(silence);
                }
                currentFrame = frame;
            }

            if (rb.Count != 0)
            {
                int newLen = buffer.Length;
                if (newLen > rb.Count) newLen = rb.Count;
                rb.Read(newLen).CopyTo(buffer, 0);
                ADPCM4BIT.VolumeInfo vi = currentFrame.VolumeInfo;
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