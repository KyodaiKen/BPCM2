using System;

namespace PCM.ADPCM
{
    public struct State
    {
        public object valprev;
        public object index;
    }

    public interface IADPCM
    {
        public State[] State { get; set; }
        public bool ChannelCoupling { get; set; }
        public double DecodingVolume { get; set; }

        public byte[] Decode(byte[] buffer);

        public byte[] Encode(byte[] buffer);
    }

#warning complete this

    public class IntelFourBit : IADPCM
    {
        public State[] State { get; set; }
        public bool ChannelCoupling { get; set; }
        public double DecodingVolume { get; set; }

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

        public IntelFourBit(int numChannels, bool channelCoupling = false)
        {
            State = new State[numChannels];
            ChannelCoupling = channelCoupling;
        }

        public byte[] Encode(byte[] buffer)
        {
            long sample_align = State.LongLength * 2;
            long i, ch;
            byte[] outBuff = new byte[State.LongLength / 2];

            for (i = 0; i <= buffer.LongLength - sample_align; i += sample_align)
            {
            }

            throw new NotImplementedException();
        }

        public byte[] Decode(byte[] buffer)
        {
            throw new NotImplementedException();
        }
    }
}