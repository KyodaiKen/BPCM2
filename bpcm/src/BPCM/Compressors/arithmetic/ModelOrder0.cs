namespace BPCM
{
    public class ModelOrder0 : AbstractModel
    {
        protected uint[] mCumCount = new uint[257];
        protected uint mTotal;

        public ModelOrder0()
        {
            mTotal = 257; // 256 + escape symbol for termination

            // initialize probabilities with 1
            for (uint i = 0; i < 257; i++)
                mCumCount[i] = 1;
        }

        protected override void Encode()
        {
            int readInt = 0;
            while (readInt != -1) // EOF
            {
                byte symbol;
                // read symbol
                readInt = mSource.ReadByte();
                symbol = (byte)readInt;

                if (readInt != -1) // EOF
                {
                    uint low_count = 0;

                    // cumulate frequencies to find the appropriate subinterval in subdivide action
                    for (byte j = 0; j < symbol; j++)
                    {
                        low_count += mCumCount[j];
                    }

                    // encode symbol
                    mAC.Encode(low_count, low_count + mCumCount[symbol], mTotal);

                    // update model => adaptive encoding model
                    mCumCount[symbol]++;
                    mTotal++;
                }
            }

            // write escape symbol ($ in docs) for termination
            mAC.Encode(mTotal - 1, mTotal, mTotal);
        }

        protected override void Decode()
        {
            uint symbol; // uint instead of byte because it must be able to contain 256 as terminator
            do
            {
                uint value;

                // read value
                value = mAC.DecodeTarget(mTotal);

                uint low_count = 0;
                // determine symbol
                for (symbol = 0; symbol <= 256 && low_count + mCumCount[symbol] <= value; symbol++)
                    low_count += mCumCount[symbol];

                // Write symbol, if it was not terminator
                if (symbol < 256)
                    mTarget.WriteByte((byte)symbol);

                if (symbol > 256) symbol = 256;

                // adapt decoder
                mAC.Decode(low_count, low_count + mCumCount[symbol]);

                // update model
                mCumCount[symbol]++;
                mTotal++;
            } while (symbol != 256); // until terminator
        }
    }
}