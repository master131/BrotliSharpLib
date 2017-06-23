using size_t = BrotliSharpLib.Brotli.SizeT;
using System.Collections.Generic;

namespace BrotliSharpLib
{
    public static partial class Brotli {
        private abstract unsafe class Hasher {
            public abstract size_t HashMemAllocInBytes(BrotliEncoderParams* params_, bool one_shot, size_t input_size);
            public abstract void Initialize(HasherHandle handle, BrotliEncoderParams* params_);
            public abstract void Prepare(HasherHandle handle, bool one_shot, size_t input_size, byte* data);
            public abstract size_t StoreLookahead();
            public abstract void Store(HasherHandle handle, byte* data, size_t mask, size_t ix);
            public abstract void StitchToPreviousBlock(HasherHandle handle, size_t num_bytes, size_t position,
                byte* ringbuffer, size_t ringbuffer_mask);
        }
    }
}
