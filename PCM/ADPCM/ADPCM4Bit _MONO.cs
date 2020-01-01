using System;

namespace PCM.ADPCM
{
    public class ADPCM4BIT_MONO
    {
        /* Intel ADPCM4BIT step variation table */

        private short[] indexTable =
        {
            -1, -1, -1, -1, 2, 4, 6, 8,
            -1, -1, -1, -1, 2, 4, 6, 8,
        };

        private short[] stepsizeTable =
        {
            7, 8, 9, 10, 11, 12, 13, 14, 16, 17,
            19, 21, 23, 25, 28, 31, 34, 37, 41, 45,
            50, 55, 60, 66, 73, 80, 88, 97, 107, 118,
            130, 143, 157, 173, 190, 209, 230, 253, 279, 307,
            337, 371, 408, 449, 494, 544, 598, 658, 724, 796,
            876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066,
            2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358,
            5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899,
            15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767
        };

        private struct StateMono
        {
            public short valprev;
            public byte index;
        }

        private StateMono s;

        public void resetState()
        {
            s = new StateMono();
        }

        public byte[] encode(byte[] pcmIn)
        {
            byte[] Output;
            Output = new byte[(pcmIn.Length) / 4 + 3];
            uint i;
            short s;

            int sign;           // Current adpcm sign bit
            int delta;          // Current adpcm output value
            int step;           // Stepsize
            int valprev;        // virtual previous output value
            int vpdiff;         // Current change to valprev
            int index;          // Current step change index
            byte bitbuffer = 0; // place to keep previous 4-bit value

            valprev = this.s.valprev;
            index = this.s.index;

            step = stepsizeTable[index];

            for (i = 0; i <= pcmIn.Length - 4; i += 4)
            {
                s = BitConverter.ToInt16(pcmIn, (int)i);

                //**** Step 1 - compute difference with previous value
                delta = s - valprev;
                sign = (delta < 0) ? 8 : 0;
                if (sign == 8) delta = -delta;

                //**** Step 2 - Divide and clamp
                delta = (int)Math.Round((double)(delta << 2) / (double)step);
                if (delta > 7) delta = 7;
                vpdiff = (delta * step) >> 2;

                //**** Step 3 - Update previous value
                if (sign == 8)
                    valprev -= vpdiff;
                else
                    valprev += vpdiff;

                //**** Step 4 - Clamp previous value to 16 bits
                if (valprev > short.MaxValue)
                    valprev = short.MaxValue;
                else if (valprev < short.MinValue)
                    valprev = short.MinValue;

                //**** Step 5 - Assemble value, update index and step values
                delta = delta | sign;
                index += indexTable[delta];
                if (index < 0) index = 0;
                if (index > 88) index = 88;
                step = stepsizeTable[index];
                //**** Step 6 - Writing values into the array
                bitbuffer = (byte)(delta << 4);

                //NEXT SAMPLE
                s = BitConverter.ToInt16(pcmIn, (int)i + 2);

                //**** Step 1 - compute difference with previous value
                delta = s - valprev;
                sign = (delta < 0) ? 8 : 0;
                if (sign == 8) delta = -delta;

                //**** Step 2 - Divide and clamp
                delta = (int)Math.Round((double)(delta << 2) / (double)step);
                if (delta > 7) delta = 7;
                vpdiff = (delta * step) >> 2;

                //**** Step 3 - Update previous value
                if (sign == 8)
                    valprev -= vpdiff;
                else
                    valprev += vpdiff;

                //**** Step 4 - Clamp previous value to 16 bits
                if (valprev > short.MaxValue)
                    valprev = short.MaxValue;
                else if (valprev < short.MinValue)
                    valprev = short.MinValue;

                //**** Step 5 - Assemble value, update index and step values
                delta = delta | sign;
                index += indexTable[delta];
                if (index < 0) index = 0;
                if (index > 88) index = 88;
                step = stepsizeTable[index];

                //**** Step 6 - Writing values into the array
                Output[i / 4 + 3] = (byte)(delta | bitbuffer);
            }

            //Splitting Values to Byte-Array

            Output[0] = BitConverter.GetBytes(this.s.valprev)[0];
            Output[1] = BitConverter.GetBytes(this.s.valprev)[1];
            Output[2] = this.s.index;

            //Update state
            this.s.valprev = (short)valprev;
            this.s.index = (byte)index;

            //Output
            return Output;
        }

        public byte[] decode(byte[] adpcmIn, out VolumeInfo vi, bool enableInloopVolumeStats, float volume = 1f)
        {
            uint i, pO;
            byte[] Output = new byte[(adpcmIn.Length - 3) * 4];

            int t;
            int sign;       // Current adpcm sign bit
            int delta;      // Current adpcm output value Mid
            int step;       // Stepsize
            int valprev;    // virtual previous output value Mid
            int vpdiff;     // Current change to valprev Side
            int index;      // Current step change index Mid
            int bitbuffer;  // place to keep next 4-bit value

            uint pv = 0; //peak volume
            uint av = 0; //average volume
            vi = new VolumeInfo();
            vi.dbPeak = double.NegativeInfinity;
            vi.dbAvg = double.NegativeInfinity;

            try
            {
                valprev = BitConverter.ToInt16(adpcmIn, 0);

                index = adpcmIn[2];
                step = stepsizeTable[index];

                for (i = 3; i <= adpcmIn.Length - 1; i++)
                {
                    pO = (i - 3) * 4;

                    //**** Step 1 - get the delta value
                    bitbuffer = adpcmIn[i];
                    delta = bitbuffer >> 4;

                    //**** Step 2 - Find new index value (for later)
                    index += indexTable[delta];
                    if (index < 0)
                        index = 0;
                    else if (index > 88)
                        index = 88;

                    //**** Step 3 - Separate sign and magnitude
                    sign = delta & 8;
                    delta = delta & 7;

                    //**** Step 4 - update output value
                    vpdiff = (delta * step) >> 2;
                    if (sign == 8)
                        valprev -= vpdiff;
                    else
                        valprev += vpdiff;

                    //**** Step 5 - clamp output value
                    if (valprev > short.MaxValue)
                        valprev = short.MaxValue;
                    else if (valprev < short.MinValue)
                        valprev = short.MinValue;

                    //**** Step 6 - Update step value
                    step = stepsizeTable[index];

                    //Volume processing (crude but it works!!)
                    t = valprev;
                    if (volume != 1)
                    {
                        t = (int)Math.Round(t * volume, 0);
                    }

                    if (t < Int16.MinValue) t = Int16.MinValue;
                    if (t > Int16.MaxValue) t = Int16.MaxValue;
                    BitConverter.GetBytes((short)t).CopyTo(Output, pO);

                    //NEXT SAMPLE

                    //**** Step 1 - get the delta value
                    delta = bitbuffer & 0xF;

                    //**** Step 2 - Find new index value (for later)
                    index += indexTable[delta];
                    if (index < 0)
                        index = 0;
                    else if (index > 88)
                        index = 88;

                    //**** Step 3 - Separate sign and magnitude
                    sign = delta & 8;
                    delta = delta & 7;

                    //**** Step 4 - update output value
                    vpdiff = (delta * step) >> 2;
                    if (sign == 8)
                        valprev -= vpdiff;
                    else
                        valprev += vpdiff;

                    //**** Step 5 - clamp output value
                    if (valprev > short.MaxValue)
                        valprev = short.MaxValue;
                    else if (valprev < short.MinValue)
                        valprev = short.MinValue;

                    //**** Step 6 - Update step value
                    step = stepsizeTable[index];
                    t = valprev;

                    //If desired, do the audio analyisis
                    //Inloop volume analysis!
                    if (enableInloopVolumeStats)
                    {
                        //Converting to absolute value
                        //Abs and conversion may be slow, so we do this once and write it into variables
                        uint tt = (uint)Math.Abs(t);

                        //determine peak value
                        if (pv < tt) pv = tt;

                        //calculating average volume
                        av = (uint)Math.Round((av + tt * 2) / 3d);
                    }

                    //Volume processing (crude but it works!!)
                    if (volume != 1)
                    {
                        t = (int)Math.Round(t * volume, 0);
                    }

                    if (t < Int16.MinValue) t = Int16.MinValue;
                    if (t > Int16.MaxValue) t = Int16.MaxValue;

                    BitConverter.GetBytes((short)t).CopyTo(Output, pO + 2);
                }

                if (enableInloopVolumeStats)
                {
                    vi.dbPeak = 20 * Math.Log10((double)pv / short.MaxValue);
                    vi.dbAvg = 20 * Math.Log10((double)av / short.MaxValue);
                }
            }
            catch { }
            return Output;
        }
    }
}