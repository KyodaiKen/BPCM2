using System.IO;

namespace PCM.Arithmetic
{
    public class ArithmeticCoderCS
    {
        // constants to split the number space of 32 bit integers
        // most significant bit kept free to prevent overflows
        public const uint g_FirstQuarter = 0x20000000;

        public const uint g_Half = 0x40000000;
        public const uint g_ThirdQuarter = 0x60000000;

        protected byte mBitBuffer;
        protected byte mBitCount;
        protected Stream mStream;
        protected uint mLow;
        protected uint mHigh;
        protected uint mStep;
        protected uint mScale;
        protected uint mBuffer;

        public ArithmeticCoderCS()
        {
            mBitBuffer = 0;
            mBitCount = 0;

            mLow = 0;
            mHigh = 0x7FFFFFFF; // just work with least significant 31 bits
            mScale = 0;
            mStep = 0;

            mBuffer = 0;
        }

        public void SetStream(Stream stream)
        {
            mStream = stream;
        }

        public void Encode(uint low_count, uint high_count, uint total)
        // total < 2^29
        {
            // partition number space into single steps
            mStep = (mHigh - mLow + 1) / total; // interval open at the top => +1

            // update upper bound
            mHigh = mLow + mStep * high_count - 1; // interval open at the top => -1

            // update lower bound
            mLow = mLow + mStep * low_count;

            // Output and Expand, Subdivide in ModelOrder0.Encode
            while ((mHigh < g_Half) || (mLow >= g_Half))
            {
                if (mHigh < g_Half)
                {
                    SetBit(0);
                    mLow = mLow * 2;
                    mHigh = mHigh * 2 + 1;

                    // Reseting recalls set by follow actions
                    for (; mScale > 0; mScale--)
                        SetBit(1);
                }
                else if (mLow >= g_Half)
                {
                    SetBit(1);
                    mLow = (mLow - g_Half) * 2;
                    mHigh = (mHigh - g_Half) * 2 + 1;

                    // Reseting recalls set by follow actions
                    for (; mScale > 0; mScale--)
                        SetBit(0);
                }
            }

            // Set an recall to be applied later
            while ((g_FirstQuarter <= mLow) && (mHigh < g_ThirdQuarter))
            {
                // keep necessary mappings in mind
                mScale++;
                mLow = (mLow - g_FirstQuarter) * 2;
                mHigh = (mHigh - g_FirstQuarter) * 2 + 1;
            }
        }

        public void EncodeFinish()
        {
            // There are two possibilities of how mLow and mHigh can be distributed,
            // which means that two bits are enough to distinguish them.

            if (mLow < g_FirstQuarter) // mLow < FirstQuarter < Half <= mHigh
            {
                SetBit(0);

                for (int i = 0; i < mScale + 1; i++) // Reseting recalls in mind
                    SetBit(1);
            }
            else // mLow < Half < ThirdQuarter <= mHigh
            {
                SetBit(1); // zeros added automatically by the decoder; no need to send them
            }

            // empty the output buffer
            SetBitFlush();
        }

        public void DecodeStart()
        {
            // empty the output buffer
            for (int i = 0; i < 31; i++) // just use the 31 least significant bits
                mBuffer = (mBuffer << 1) | GetBit();
        }

        // converts raw stored data to original value
        public uint DecodeTarget(uint total)
        // total < 2^29
        {
            // split number space into single steps
            mStep = (mHigh - mLow + 1) / total;

            // return current value
            return (mBuffer - mLow) / mStep;
        }

        public void Decode(uint low_count, uint high_count)
        {
            // update upper bound
            mHigh = mLow + high_count * mStep - 1;

            // update lower bound
            mLow = mLow + low_count * mStep;

            // Output and Expand, Subdivide in ModelOrder0.Decode, to get synced with Encoder
            while ((mHigh < g_Half) || (mLow >= g_Half))
            {
                if (mHigh < g_Half)
                {
                    mLow = mLow * 2;
                    mHigh = mHigh * 2 + 1;
                    mBuffer = mBuffer * 2 + GetBit();
                }
                else if (mLow >= g_Half)
                {
                    mLow = (mLow - g_Half) * 2;
                    mHigh = (mHigh - g_Half) * 2 + 1;
                    mBuffer = (mBuffer - g_Half) * 2 + GetBit();
                }
                mScale = 0;
            }

            // Set an recall to be applied later
            while ((g_FirstQuarter <= mLow) && (mHigh < g_ThirdQuarter))
            {
                mScale++;
                mLow = (mLow - g_FirstQuarter) * 2;
                mHigh = (mHigh - g_FirstQuarter) * 2 + 1;
                mBuffer = (mBuffer - g_FirstQuarter) * 2 + GetBit();
            }
        }

        protected void SetBit(uint bit)
        {
            // add bit to the buffer
            mBitBuffer = (byte)(((uint)mBitBuffer << 1) | bit);
            mBitCount++;

            if (mBitCount == 8) // buffer full
            {
                // write
                mStream.WriteByte(mBitBuffer);
                mBitCount = 0;
            }
        }

        protected void SetBitFlush()
        {
            // fill buffer with 0 up to the next byte
            while (mBitCount != 0)
            {
                SetBit(0);
            }
        }

        protected uint GetBit()
        {
            if (mBitCount == 0) // buffer empty
            {
                int readInt = mStream.ReadByte();
                if (readInt == -1) // EOF = Is stream read completely?
                    mBitBuffer = 0;
                else
                    mBitBuffer = (byte)readInt; // append zeros

                mBitCount = 8;
            }

            // extract bit from buffer
            uint bit = (uint)mBitBuffer >> 7;
            mBitBuffer = (byte)((uint)mBitBuffer << 1);
            mBitCount--;

            return bit;
        }
    }
}