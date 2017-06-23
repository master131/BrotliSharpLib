using System.Runtime.InteropServices;
using size_t = BrotliSharpLib.Brotli.SizeT;

namespace BrotliSharpLib
{
    public static partial class Brotli
    {
        private class HashLongestMatch64H6 : Hasher
        {
            public size_t HashTypeLength()
            {
                return 8;
            }

            public override size_t StoreLookahead()
            {
                return 8;
            }

            /* HashBytes is the function that chooses the bucket to place the address in. */
            private static unsafe uint HashBytes(byte* data, ulong mask, int shift)
            {
                ulong h = (*(ulong*)(data) & mask) * kHashMul64Long;
                /* The higher bits contain more mixture from the multiplication,
                   so we take our results from there. */
                return (uint)(h >> shift);
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct HashLongestMatch
            {
                /* Number of hash buckets. */
                public size_t bucket_size_;
                /* Only block_size_ newest backward references are kept,
                   and the older are forgotten. */
                public size_t block_size_;
                /* Left-shift for computing hash bucket index from hash value. */
                public int hash_shift_;
                /* Mask for selecting the next 4-8 bytes of input */
                public ulong hash_mask_;
                /* Mask for accessing entries in a block (in a ring-buffer manner). */
                public uint block_mask_;

                /* --- Dynamic size members --- */

                /* Number of entries in a particular bucket. */
                /* uint16_t num[bucket_size]; */

                /* Buckets containing block_size_ of backward references. */
                /* uint32_t* buckets[bucket_size * block_size]; */
            }

            private static unsafe HashLongestMatch* Self(HasherHandle handle)
            {
                return (HashLongestMatch*)&(GetHasherCommon(handle)[1]);
            }

            private static unsafe ushort* Num(HashLongestMatch* self)
            {
                return (ushort*)(&self[1]);
            }

            private static unsafe uint* Buckets(HashLongestMatch* self)
            {
                return (uint*)(&Num(self)[self->bucket_size_]);
            }

            public override unsafe void Initialize(HasherHandle handle, BrotliEncoderParams* params_)
            {
                HasherCommon* common = GetHasherCommon(handle);
                HashLongestMatch* self = Self(handle);
                self->hash_shift_ = 64 - common->params_.bucket_bits;
                self->hash_mask_ = (~((ulong)0U)) >> (64 - 8 * common->params_.hash_len);
                self->bucket_size_ = (size_t)1 << common->params_.bucket_bits;
                self->block_size_ = (size_t)1 << common->params_.block_bits;
                self->block_mask_ = (uint)(self->block_size_ - 1);
            }

            public override unsafe void Prepare(HasherHandle handle, bool one_shot, SizeT input_size, byte* data)
            {
                HashLongestMatch* self = Self(handle);
                ushort* num = Num(self);
                /* Partial preparation is 100 times slower (per socket). */
                size_t partial_prepare_threshold = self->bucket_size_ >> 6;
                if (one_shot && input_size <= partial_prepare_threshold)
                {
                    size_t i;
                    for (i = 0; i < input_size; ++i)
                    {
                        uint key = HashBytes(&data[i], self->hash_mask_,
                            self->hash_shift_);
                        num[key] = 0;
                    }
                }
                else
                {
                    memset(num, 0, self->bucket_size_ * sizeof(ushort));
                }
            }

            public override unsafe size_t HashMemAllocInBytes(BrotliEncoderParams* params_, bool one_shot,
                size_t input_size)
            {
                size_t bucket_size = (size_t)1 << params_->hasher.bucket_bits;
                size_t block_size = (size_t)1 << params_->hasher.block_bits;
                return Marshal.SizeOf(typeof(HashLongestMatch)) + bucket_size * (2 + 4 * block_size);
            }

            /* Look at 4 bytes at &data[ix & mask].
               Compute a hash from these, and store the value of ix at that position. */
            public override unsafe void Store(HasherHandle handle,
                byte* data, size_t mask, size_t ix)
            {
                HashLongestMatch* self = Self(handle);
                ushort* num = Num(self);
                uint key = HashBytes(&data[ix & mask], self->hash_mask_,
                    self->hash_shift_);
                size_t minor_ix = num[key] & self->block_mask_;
                size_t offset =
                    minor_ix + (key << GetHasherCommon(handle)->params_.block_bits);
                Buckets(self)[offset] = (uint)ix;
                ++num[key];
            }

            public override unsafe void StitchToPreviousBlock(HasherHandle handle, size_t num_bytes, size_t position,
                byte* ringbuffer,
                size_t ringbuffer_mask)
            {
                if (num_bytes >= HashTypeLength() - 1 && position >= 3)
                {
                    /* Prepare the hashes for three last bytes of the last write.
                       These could not be calculated before, since they require knowledge
                       of both the previous and the current block. */
                    Store(handle, ringbuffer, ringbuffer_mask, position - 3);
                    Store(handle, ringbuffer, ringbuffer_mask, position - 2);
                    Store(handle, ringbuffer, ringbuffer_mask, position - 1);
                }
            }
        }
    }
}
