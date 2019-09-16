using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BPCM.ADPCM
{
    struct State
    {
        public object valprev;
        public object index;
    }

    public interface IADPCM
    {
        public State[] State;
        public bool ChannelCoupling;
        public double DecodingVolume;
        public byte[] Decode(byte[] buffer);
        public byte[] Encode(byte[] buffer);
    }

    public class IntelFourBit : IADPCM
    {
        private State[] _state; public State[] State { get => _state; set => _state = value; }
        private bool _cc; public bool ChannelCoupling { get => _cc; set => _cc = value; }
        private double _vol; public double DecodingVolume { get => _vol; set => _vol = value; }
        
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
            _state = new State[numChannels];
            _cc = channelCoupling;
        }

        public byte[] Encode(byte[] buffer)
        {
            long sample_align = _state.LongLength * 2;
            long i, ch;
            byte[] outBuff = new byte[_state.LongLength / 2];

            for (i = 0; i<=buffer.LongLength-sample_align; i+=sample_align)
            {

            }
        }
    }
}
