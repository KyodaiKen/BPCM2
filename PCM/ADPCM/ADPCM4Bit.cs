using System;

namespace PCM.ADPCM
{
    public class ADPCM4BIT
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

        private struct State
        {
            public short valprev_a;
            public short valprev_b;
            public byte index_a;
            public byte index_b;
        }

        public struct VolumeInfo
        {
            public double dbPeakL { get; set; }
            public double dbPeakR { get; set; }
            public double dbAvgR { get; set; }
            public double dbAvgL { get; set; }
        }

        private State s;
        private Random r;

        public void resetState()
        {
            s = new State();
        }

        public byte[] encode(byte[] pcmIn, bool midside)
        {
            byte[] Output;
            Output = new byte[(pcmIn.Length) / 4 + 6];
            uint i;
            short sa, sb;

            int sign;       // Current adpcm sign bit
            int delta;      // Current adpcm output value
            int step_a;     // Stepsize 1
            int step_b;     // Stepsize 2
            int valprev_a;  // virtual previous output value 1
            int valprev_b;  // virtual previous output value 2
            int vpdiff;     // Current change to valprev
            int index_a;    // Current step change index 1
            int index_b;    // Current step change index 2
            byte bitbuffer = 0; // place to keep previous 4-bit value

            valprev_a = s.valprev_a;
            valprev_b = s.valprev_b;
            index_a = s.index_a;
            index_b = s.index_b;

            //Get first sample for the init
            /*if (valprev_a == 0 && valprev_b == 0)
            {
                if (midside)
                {
                    valprev_a = (short)(((int)(BitConverter.ToInt16(pcmIn, 0)) + (int)(BitConverter.ToInt16(pcmIn, 2))) / 2);
                    valprev_b = (short)(((int)(BitConverter.ToInt16(pcmIn, 0)) - (int)(BitConverter.ToInt16(pcmIn, 2))) / 2);
                }
                else
                {
                    valprev_a = BitConverter.ToInt16(pcmIn, 0);
                    valprev_b = BitConverter.ToInt16(pcmIn, 2);
                }

                index_a = (int)Math.Round(((double)valprev_a + 32767) / 736);
                index_b = (int)Math.Round(((double)valprev_b + 32767) / 736);
            }*/

            step_a = stepsizeTable[index_a];
            step_b = stepsizeTable[index_b];

            for (i = 0; i <= pcmIn.Length - 4; i += 4)
            {
                if (midside)
                {
                    sa = (short)(((int)(BitConverter.ToInt16(pcmIn, (int)i)) + (int)(BitConverter.ToInt16(pcmIn, (int)i + 2))) / 2);
                    sb = (short)(((int)(BitConverter.ToInt16(pcmIn, (int)i)) - (int)(BitConverter.ToInt16(pcmIn, (int)i + 2))) / 2);
                }
                else
                {
                    sa = BitConverter.ToInt16(pcmIn, (int)i);
                    sb = BitConverter.ToInt16(pcmIn, (int)i + 2);
                }

                //.--------------------------------.
                //|               Ch1              |
                //'--------------------------------'
                //**** Step 1 - compute difference with previous value
                delta = sa - valprev_a;
                sign = (delta < 0) ? 8 : 0;
                if (sign == 8) delta = -delta;

                //**** Step 2 - Divide and clamp
                delta = (int)Math.Round((delta * 4) / (double)step_a);
                if (delta > 7) delta = 7;
                vpdiff = (int)Math.Round((delta * step_a) / 4d);

                //**** Step 3 - Update previous value
                if (sign == 8)
                    valprev_a -= vpdiff;
                else
                    valprev_a += vpdiff;

                //**** Step 4 - Clamp previous value to 16 bits
                if (valprev_a > short.MaxValue)
                    valprev_a = short.MaxValue;
                else if (valprev_a < short.MinValue)
                    valprev_a = short.MinValue;

                //**** Step 5 - Assemble value, update index and step values
                delta = delta | sign;
                index_a += indexTable[delta];
                if (index_a < 0) index_a = 0;
                if (index_a > 88) index_a = 88;
                step_a = stepsizeTable[index_a];

                //**** Step 6 - Writing values into the array
                bitbuffer = (byte)(delta << 4);

                //.--------------------------------.
                //|               Ch2              |
                //'--------------------------------'
                //**** Step 1 - compute difference with previous value
                delta = sb - valprev_b;
                sign = (delta < 0) ? 8 : 0;
                if (sign == 8) delta = -delta;

                //**** Step 2 - Divide and clamp
                delta = (int)Math.Round((delta * 4) / (double)step_b);
                if (delta > 7) delta = 7;
                vpdiff = (int)Math.Round((delta * step_b) / 4d);

                //**** Step 3 - Update previous value
                if (sign == 8)
                    valprev_b -= vpdiff;
                else
                    valprev_b += vpdiff;

                //**** Step 4 - Clamp previous value to 16 bits
                if (valprev_b > short.MaxValue)
                    valprev_b = short.MaxValue;
                else if (valprev_b < short.MinValue)
                    valprev_b = short.MinValue;

                //**** Step 5 - Assemble value, update index and step values
                delta = delta | sign;
                index_b += indexTable[delta];
                if (index_b < 0) index_b = 0;
                if (index_b > 88) index_b = 88;
                step_b = stepsizeTable[index_b];

                //**** Step 6 - Writing values into the array
                Output[i / 4 + 6] = (byte)(delta | bitbuffer);
            }

            //Splitting Values to Byte-Array

            Output[0] = BitConverter.GetBytes(s.valprev_a)[0];
            Output[1] = BitConverter.GetBytes(s.valprev_a)[1];

            Output[2] = BitConverter.GetBytes(s.valprev_b)[0];
            Output[3] = BitConverter.GetBytes(s.valprev_b)[1];

            Output[4] = s.index_a;
            Output[5] = s.index_b;

            //Update state
            s.valprev_a = (short)valprev_a;
            s.index_a = (byte)index_a;
            s.valprev_b = (short)valprev_b;
            s.index_b = (byte)index_b;

            //Output
            return Output;
        }

        public byte[] decode(byte[] adpcmIn, out VolumeInfo vi, bool midside, bool enableInloopVolumeStats, bool enableDither, float volume = 1f)
        {
            int ta, tb;
            uint i, pO;
            byte[] Output = new byte[(adpcmIn.Length - 6) * 4];

            int sign;       // Current adpcm sign bit
            int delta;      // Current adpcm output value Mid
            int step_a;     // Stepsize Mid
            int step_b;     // Stepsize Side
            double valprev_a;  // virtual previous output value Mid
            double valprev_b;  // virtual previous output value Side
            double vpdiff;     // Current change to valprev Side
            int index_a;    // Current step change index Mid
            int index_b;    // Current step change index Side
            int bitbuffer;  // place to keep next 4-bit value

            uint pa = 0, pb = 0; //peak volume
            uint aa = 0, ab = 0; //average volume

            vi = new VolumeInfo();
            vi.dbPeakL = double.NegativeInfinity;
            vi.dbPeakR = double.NegativeInfinity;
            vi.dbAvgL = double.NegativeInfinity;
            vi.dbAvgR = double.NegativeInfinity;

            if (enableDither) r = new Random();

            try
            {
                valprev_a = BitConverter.ToInt16(adpcmIn, 0);
                valprev_b = BitConverter.ToInt16(adpcmIn, 2);
                index_a = adpcmIn[4];
                index_b = adpcmIn[5];
                step_a = stepsizeTable[index_a];
                step_b = stepsizeTable[index_b];

                for (i = 6; i <= adpcmIn.Length - 1; i++)
                {
                    //.--------------------------------.
                    //|              Ch0               |
                    //'--------------------------------'

                    //**** Step 1 - get the delta value
                    bitbuffer = adpcmIn[i];
                    delta = bitbuffer >> 4;

                    //**** Step 2 - Find new index value (for later)
                    index_a += indexTable[delta];
                    if (index_a < 0)
                        index_a = 0;
                    else if (index_a > 88)
                        index_a = 88;

                    //**** Step 3 - Separate sign and magnitude
                    sign = delta & 8;
                    delta = delta & 7;

                    //**** Step 4 - update output value
                    //vpdiff = (delta * step_a) >> 2;
                    vpdiff = (delta * step_a) / 4d;
                    if (sign == 8)
                        valprev_a -= vpdiff;
                    else
                        valprev_a += vpdiff;

                    //**** Step 5 - Update step value
                    step_a = stepsizeTable[index_a];

                    //.--------------------------------.
                    //|              Ch1               |
                    //'--------------------------------'

                    //**** Step 1 - get the delta value
                    delta = bitbuffer & 0xF;

                    //**** Step 2 - Find new index value (for later)
                    index_b += indexTable[delta];
                    if (index_b < 0)
                        index_b = 0;
                    else if (index_b > 88)
                        index_b = 88;

                    //**** Step 3 - Separate sign and magnitude
                    sign = delta & 8;
                    delta = delta & 7;

                    //**** Step 4 - update output value
                    //vpdiff = (delta * step_b) >> 2;
                    vpdiff = (delta * step_b) / 4d;
                    if (sign == 8)
                        valprev_b -= vpdiff;
                    else
                        valprev_b += vpdiff;

                    //**** Step 5 - Update step value
                    step_b = stepsizeTable[index_b];

                    //Output the samples with random dither pattern if requested
                    if (enableDither)
                    {
                        bool up = r.Next(0, 2) == 1;
                        if (up)
                        {
                            if (midside)
                            {
                                ta = (int)Math.Ceiling(valprev_a + valprev_b);
                                tb = (int)Math.Ceiling(valprev_a - valprev_b);
                            }
                            else
                            {
                                ta = (int)Math.Ceiling(valprev_a);
                                tb = (int)Math.Ceiling(valprev_b);
                            }
                        }
                        else
                        {
                            if (midside)
                            {
                                ta = (int)Math.Floor(valprev_a + valprev_b);
                                tb = (int)Math.Floor(valprev_a - valprev_b);
                            }
                            else
                            {
                                ta = (int)Math.Floor(valprev_a);
                                tb = (int)Math.Floor(valprev_b);
                            }
                        }
                    }
                    else
                    {
                        if (midside)
                        {
                            ta = (int)Math.Round(valprev_a + valprev_b, 0);
                            tb = (int)Math.Round(valprev_a - valprev_b, 0);
                        }
                        else
                        {
                            ta = (int)Math.Round(valprev_a, 0);
                            tb = (int)Math.Round(valprev_b, 0);
                        }
                    }

                    //Clamp samples
                    if (ta < Int16.MinValue) ta = Int16.MinValue;
                    if (ta > Int16.MaxValue) ta = Int16.MaxValue;
                    if (tb < Int16.MinValue) tb = Int16.MinValue;
                    if (tb > Int16.MaxValue) tb = Int16.MaxValue;

                    //If desired, do the audio analyisis
                    //Inloop volume analysis!
                    if (enableInloopVolumeStats)
                    {
                        //Converting to absolute value
                        //Abs and conversion may be slow, so we do this once and write it into variables
                        uint tta = (uint)Math.Abs(ta)
                           , ttb = (uint)Math.Abs(tb);

                        //determine peak value
                        if (pa < tta) pa = tta;
                        if (pb < ttb) pb = ttb;

                        //calculating average volume
                        aa = (uint)Math.Round((aa + tta * 2) / 3d);
                        ab = (uint)Math.Round((ab + ttb * 2) / 3d);
                    }

                    //Volume processing (crude but it works!!)
                    if (volume != 1)
                    {
                        ta = (int)Math.Round(ta * volume, 0);
                        tb = (int)Math.Round(tb * volume, 0);
                    }

                    //More clamping
                    if (ta < Int16.MinValue) ta = Int16.MinValue;
                    if (ta > Int16.MaxValue) ta = Int16.MaxValue;
                    if (tb < Int16.MinValue) tb = Int16.MinValue;
                    if (tb > Int16.MaxValue) tb = Int16.MaxValue;

                    pO = (i - 6) * 4;
                    BitConverter.GetBytes((short)ta).CopyTo(Output, pO);
                    BitConverter.GetBytes((short)tb).CopyTo(Output, pO + 2);
                }

                //If the volume analysis was done, fill the object provided with the data
                if (enableInloopVolumeStats)
                {
                    vi.dbPeakL = 20 * Math.Log10((double)pa / short.MaxValue);
                    vi.dbPeakR = 20 * Math.Log10((double)pb / short.MaxValue);
                    vi.dbAvgL = 20 * Math.Log10((double)aa / short.MaxValue);
                    vi.dbAvgR = 20 * Math.Log10((double)ab / short.MaxValue);
                }
            }
            catch { }
            r = null;
            return Output;
        }
    }
}