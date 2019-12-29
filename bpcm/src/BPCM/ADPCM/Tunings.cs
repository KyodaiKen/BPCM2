using System;

namespace BPCM.ADPCM
{
    internal static class Tunings
    {
        public enum Silence : byte
        {
            FullSignal = 0,
            PartiallySilent = 127,
            TotalSilence = 255
        }

        public enum SilenceAt : byte
        {
            Beginning = 0,
            End = 255,
            NotApplicable = 127
        }

        public struct SilenceInfoBean
        {
            public Silence is_silent;
            public SilenceAt silence_from;
            public int buffer_pos;
        }

        public static bool IsTotallySilent(byte[] input)
        {
            //loop through all samples, channel layout doesn't matter, here. Just a quick and dirty solution to be very fast.
            short first = BitConverter.ToInt16(input, 0);
            for (int i = 2; i < input.Length - 1; i += 2) if (BitConverter.ToInt16(input, i) != first) return false;
            return true;
        }

        public static SilenceInfoBean GetSilenceInfo(byte[] input, short treshold = 8, short max_delay = 10)
        {
            //Initialize info object
            SilenceInfoBean ret_info = new SilenceInfoBean();
            //Check silence from the beginning of the buffer
            if (input.Length < 4) return ret_info;
            short first = BitConverter.ToInt16(input, 0); //DC offset
            int i;
            for (i = 2; i < input.Length - 2; i += 2)
            {
                if (Math.Abs(BitConverter.ToInt16(input, i) - first) > treshold)
                {
                    if (i >= max_delay * 2)
                    {
                        ret_info.is_silent = Silence.PartiallySilent;
                        ret_info.silence_from = SilenceAt.Beginning;
                        ret_info.buffer_pos = i - 1;
                        return ret_info;
                    }
                    else
                    {
                        ret_info.is_silent = Silence.FullSignal;
                        ret_info.silence_from = SilenceAt.NotApplicable;
                        ret_info.buffer_pos = -1;
                        break;
                    }
                }
            }

            //When the buffer was fully checked, there is no signal in it.
            if (i == input.Length - 2)
            {
                ret_info.is_silent = Silence.TotalSilence;
                ret_info.silence_from = SilenceAt.NotApplicable;
                ret_info.buffer_pos = 0;
                return ret_info;
            }

            //No silence found from the beginning, now searching from the end
            if (ret_info.is_silent == Silence.FullSignal)
            {
                first = BitConverter.ToInt16(input, input.Length - 2);
                for (i = input.Length - 4; i >= 0; i -= 2)
                {
                    if (Math.Abs(BitConverter.ToInt16(input, i) - first) > treshold)
                    {
                        if (input.Length - i <= max_delay * 2)
                        {
                            break;
                        }
                        else
                        {
                            ret_info.is_silent = Silence.PartiallySilent;
                            ret_info.silence_from = SilenceAt.End;
                            ret_info.buffer_pos = i;
                            return ret_info;
                        }
                    }
                }
            }

            return ret_info;
        }
    }
}