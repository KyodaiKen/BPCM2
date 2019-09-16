using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace BPCM
{
    public enum ModeE
    {
        MODE_ENCODE=0,
        MODE_DECODE=1
    }

    public abstract class AbstractModel
    {
        public void Process(Stream source, Stream target, ModeE mode)
        {
            mSource = source;
            mTarget = target;

            if (mode == ModeE.MODE_ENCODE)
            {
                mAC.SetStream(mTarget);

                // encode
                Encode();

                mAC.EncodeFinish();
            }
            else // mode == ModeE.MODE_DECODE
            {
                mAC.SetStream(mSource);

                mAC.DecodeStart();

                // decode
                Decode();
            }
        }

        protected abstract void Encode();
	    protected abstract void Decode();

	    protected ArithmeticCoderCS mAC = new ArithmeticCoderCS();
        protected Stream mSource;
        protected Stream mTarget;
    }
}
