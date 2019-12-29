using System;
using System.IO;

namespace BPCM.PCMContainers.RIFFWave
{
    public struct WAVEFormat
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSeconds;
        public ushort nBlockAlign;
        public ushort nBitsPerSample;
    }

    internal class WAVEReader
    {
        public struct WAVEInfo
        {
            public uint RIFFChunkSize;
            public uint fmtChunkSize;
            public uint PCMDataOffset;
            public uint PCMDataLength;
            public WAVEFormat fmtHeader;
            public TimeSpan Duration { get { return TimeSpan.FromSeconds(PCMDataLength / (double)(fmtHeader.nSamplesPerSec * fmtHeader.nChannels * (fmtHeader.nBitsPerSample / 8))); } }
        }

        private Stream s;
        private WAVEInfo wi;
        private bool initialized;

        public WAVEInfo Info { get { return this.wi; } }
        public long Position { get { return this.s.Position; } }
        public long PCMPosition { get { return this.s.Position - this.wi.PCMDataOffset; } }

        public WAVEReader(Stream s)
        {
            if (s != null) this.s = s; else throw new Exception("WAVEReader: Stream was not initialized!");
            BinaryReader br = new BinaryReader(this.s);

            //Check for RIFF chunk
            if (br.ReadUInt32() != 1179011410u) throw new Exception("WAVEReader: RIFF header missing!");

            this.wi.RIFFChunkSize = br.ReadUInt32();

            if (this.wi.RIFFChunkSize > this.s.Length) Console.WriteLine("WAVEReader: WARNING! RIFF chunk is actually larger than the available Data!");

            //Check RIFF type
            if (br.ReadUInt32() != 1163280727u) throw new Exception("WAVEReader: The RIFF container seems not to be a RIFF-WAVE container!");

            //Find 'fmt ' chunk
            bool found = false;
            while ((this.s.Position <= this.s.Length) && found == false)
            {
                if (br.ReadUInt32() != 544501094u) this.s.Position -= 3; else { found = true; break; }
            }

            this.wi.fmtChunkSize = br.ReadUInt32();

            //Parse 'fmt ' chunk data
            this.wi.fmtHeader.wFormatTag = br.ReadUInt16();
            this.wi.fmtHeader.nChannels = br.ReadUInt16();
            this.wi.fmtHeader.nSamplesPerSec = br.ReadUInt32();
            this.wi.fmtHeader.nAvgBytesPerSeconds = br.ReadUInt32();
            this.wi.fmtHeader.nBlockAlign = br.ReadUInt16();
            this.wi.fmtHeader.nBitsPerSample = br.ReadUInt16();

            found = false;
            //Find 'data' chunk
            while ((this.s.Position <= this.s.Length) && found == false)
            {
                if (br.ReadUInt32() != 1635017060u) this.s.Position -= 3; else { found = true; break; }
            }

            this.wi.PCMDataLength = br.ReadUInt32();
            this.wi.PCMDataOffset = (uint)this.s.Position;

            if (this.wi.PCMDataLength < this.s.Length - this.wi.PCMDataOffset)
            {
                Console.WriteLine("WAVEReader: WARNING! Data chunk is smaller than the stream!");
                Console.WriteLine("WAVEReader: Assuming data chunk size to be correct.");
            }
            else if (this.wi.PCMDataLength == (uint)this.s.Length - this.wi.PCMDataOffset)
            {
                //Nothing to do here, everything is fine
            }
            else
            {
                Console.WriteLine("WAVEReader: WARNING! Data chunk is bigger than the stream!");
                this.wi.PCMDataLength = (uint)this.s.Length - this.wi.PCMDataOffset;
                Console.WriteLine("WAVEReader: Data chunk size was corrected.");
            }

            initialized = true;
        }

        //Reads into a byte array
        public bool Read(out byte[] buffer, uint lengthMS)
        {
            if (!initialized) { buffer = null; throw new Exception("WAVEReader was not initialized properly."); }
            long length = (long)((lengthMS / 1000.0) * wi.fmtHeader.nSamplesPerSec * wi.fmtHeader.nChannels * (wi.fmtHeader.nBitsPerSample / 8));
            //Fix end silence bug by checking for past EOS here
            if (s.Position + length > s.Length) length = s.Length - s.Position;
            buffer = new byte[length];
            s.Read(buffer, 0, buffer.Length);
            return true;
        }

        //Reads it as an array of the data type used in the PCM data rather than just a byte array
        public bool Read(out Array buffer, uint lengthMS)
        {
            if (!initialized) { buffer = null; throw new Exception("WAVEReader was not initialized properly."); }
            Read(out byte[] rawBuffer, lengthMS);
            long length = rawBuffer.LongLength / wi.fmtHeader.nChannels / (wi.fmtHeader.nBitsPerSample / 8);
            buffer = null;
            switch (wi.fmtHeader.wFormatTag)
            {
                case 1:
                case 0xFFFE:
                    switch (wi.fmtHeader.nBitsPerSample)
                    {
                        case 8:
                            buffer = Array.CreateInstance(typeof(byte), length, wi.fmtHeader.nChannels - 1);
                            break;

                        case 16:
                            buffer = Array.CreateInstance(typeof(short), length, wi.fmtHeader.nChannels - 1);
                            break;

                        case 24:
                        case 32:
                            buffer = Array.CreateInstance(typeof(int), length, wi.fmtHeader.nChannels - 1);
                            break;
                    }
                    break;

                default:
                    return false;
            }
            rawBuffer.CopyTo(buffer, 0);
            return true;
        }
    }

    //Crude but very simple wave writer
    internal class WAVEWriter
    {
        private Stream s;
        private WAVEFormat wf;
        private bool initialized;
        private bool finalized;

        public WAVEFormat fmtHeader { get { return wf; } }
        public long Position { get { return s.Position; } }
        public long PCMPosition { get { return s.Position - 44; } }

        public WAVEWriter(Stream s, WAVEFormat FMTHeader)
        {
            if (s != null) this.s = s; else throw new Exception("WAVEWriter: Stream was not initialized!");
            this.s = s;
            BinaryWriter bw = new BinaryWriter(this.s);
            wf = FMTHeader;
            bw.Write(1179011410u); //RIFF
            bw.Write(0u);
            bw.Write(1163280727u); //WAVE
            bw.Write(544501094u);  //'fmt '
            bw.Write(16u);
            bw.Write(wf.wFormatTag);
            bw.Write(wf.nChannels);
            bw.Write(wf.nSamplesPerSec);
            bw.Write(wf.nAvgBytesPerSeconds);
            bw.Write(wf.nBlockAlign);
            bw.Write(wf.nBitsPerSample);
            bw.Write(1635017060u); //data
            bw.Write(0u);
            initialized = true;
            finalized = false;
        }

        public bool Write(byte[] buffer)
        {
            if (!initialized && !finalized) throw new Exception("WAVEWriter was not initialized properly or was already finalized.");
            s.Write(buffer, 0, buffer.Length);
            return true;
        }

        public bool Write(Array buffer)
        {
            if (!initialized && !finalized) throw new Exception("WAVEWriter was not initialized properly or was already finalized.");
            //Converting multidimensional any type array into bytearray!
            byte[] bytebuffer = new byte[buffer.GetUpperBound(0) * (wf.nBitsPerSample / 8) * buffer.GetUpperBound(1)];
            buffer.CopyTo(bytebuffer, 0);
            s.Write(bytebuffer, 0, bytebuffer.Length);
            return true;
        }

        public bool WriteSilence(double lengthSilenceSeconds)
        {
            if (!initialized && !finalized) throw new Exception("WAVEWriter was not initialized properly or was already finalized.");
            byte[] bytebuffer = new byte[(long)(lengthSilenceSeconds * (wf.nBitsPerSample / 8) * wf.nSamplesPerSec * wf.nChannels)];
            s.Write(bytebuffer, 0, bytebuffer.Length);
            return true;
        }

        public bool Finalize()
        {
            if (!initialized && !finalized) throw new Exception("WAVEWriter was not initialized properly or was already finalized.");
            long oldPos = PCMPosition;
            s.Seek(4L, SeekOrigin.Begin);
            BinaryWriter bw = new BinaryWriter(s);
            bw.Write((uint)(oldPos + 36));
            s.Seek(40L, SeekOrigin.Begin);
            bw.Write((uint)oldPos);
            return true;
        }
    }
}