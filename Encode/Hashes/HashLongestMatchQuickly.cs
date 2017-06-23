using System.Runtime.InteropServices;
using size_t = BrotliSharpLib.Brotli.SizeT;

namespace BrotliSharpLib {
    public static partial class Brotli {
        private class HashLongestMatchQuicklyH2 : Hasher {
            private const int BUCKET_BITS = 16;
            private const int BUCKET_SWEEP = 1;
            private const int HASH_LEN = 5;
            private const int USE_DICTIONARY = 1;
            private const int BUCKET_SIZE = 1 << BUCKET_BITS;
            private const int HASH_MAP_SIZE = 4 << BUCKET_BITS;

            public size_t HashTypeLength() {
                return 8;
            }

            public override size_t StoreLookahead() {
                return 8;
            }

            /* HashBytes is the function that chooses the bucket to place
               the address in. The HashLongestMatch and HashLongestMatchQuickly
               classes have separate, different implementations of hashing. */
            private static unsafe uint HashBytes(byte* data) {
                ulong h = ((*(ulong*) (data) << (64 - 8 * HASH_LEN)) *
                           kHashMul64);
                /* The higher bits contain more mixture from the multiplication,
                   so we take our results from there. */
                return (uint) (h >> (64 - BUCKET_BITS));
            }

            /* A (forgetful) hash table to the data seen by the compressor, to
               help create backward references to previous data.

               This is a hash map of fixed size (BUCKET_SIZE). Starting from the
               given index, BUCKET_SWEEP buckets are used to store values of a key. */
            [StructLayout(LayoutKind.Sequential)]
            private unsafe struct HashLongestMatchQuickly {
                public fixed uint buckets_[BUCKET_SIZE + BUCKET_SWEEP];
            }

            private static unsafe HashLongestMatchQuickly* Self(HasherHandle handle) {
                return (HashLongestMatchQuickly*) &(GetHasherCommon(handle)[1]);
            }

            public override unsafe void Initialize(HasherHandle handle, BrotliEncoderParams* params_) {
            }

            public override unsafe void Prepare(HasherHandle handle, bool one_shot, SizeT input_size, byte* data) {
                HashLongestMatchQuickly* self = Self(handle);
                /* Partial preparation is 100 times slower (per socket). */
                size_t partial_prepare_threshold = HASH_MAP_SIZE >> 7;
                if (one_shot && input_size <= partial_prepare_threshold) {
                    size_t i;
                    for (i = 0; i < input_size; ++i) {
                        uint key = HashBytes(&data[i]);
                        memset(&self->buckets_[key], 0, BUCKET_SWEEP * sizeof(uint));
                    }
                }
                else {
                    /* It is not strictly necessary to fill this buffer here, but
                       not filling will make the results of the compression stochastic
                       (but correct). This is because random data would cause the
                       system to find accidentally good backward references here and there. */
                    memset(&self->buckets_[0], 0, sizeof(uint) * (BUCKET_SIZE + BUCKET_SWEEP));
                }
            }

            public override unsafe size_t HashMemAllocInBytes(BrotliEncoderParams* params_, bool one_shot,
                size_t input_size) {
                return Marshal.SizeOf(typeof(HashLongestMatchQuickly));
            }

            /* Look at 5 bytes at &data[ix & mask].
               Compute a hash from these, and store the value somewhere within
               [ix .. ix+3]. */
            public override unsafe void Store(HasherHandle handle,
                byte* data, size_t mask, size_t ix) {
                uint key = HashBytes(&data[ix & mask]);
                /* Wiggle the value with the bucket sweep range. */
                uint off = (ix >> 3) % BUCKET_SWEEP;
                Self(handle)->buckets_[key + off] = (uint) ix;
            }

            public override unsafe void StitchToPreviousBlock(HasherHandle handle, size_t num_bytes, size_t position,
                byte* ringbuffer,
                size_t ringbuffer_mask) {
                if (num_bytes >= HashTypeLength() - 1 && position >= 3) {
                    /* Prepare the hashes for three last bytes of the last write.
                       These could not be calculated before, since they require knowledge
                       of both the previous and the current block. */
                    Store(handle, ringbuffer, ringbuffer_mask, position - 3);
                    Store(handle, ringbuffer, ringbuffer_mask, position - 2);
                    Store(handle, ringbuffer, ringbuffer_mask, position - 1);
                }
            }
        }

        private class HashLongestMatchQuicklyH3 : Hasher {
            private const int BUCKET_BITS = 16;
            private const int BUCKET_SWEEP = 2;
            private const int HASH_LEN = 5;
            private const int USE_DICTIONARY = 0;
            private const int BUCKET_SIZE = 1 << BUCKET_BITS;
            private const int HASH_MAP_SIZE = 4 << BUCKET_BITS;

            public size_t HashTypeLength() {
                return 8;
            }

            public override size_t StoreLookahead() {
                return 8;
            }

            /* HashBytes is the function that chooses the bucket to place
               the address in. The HashLongestMatch and HashLongestMatchQuickly
               classes have separate, different implementations of hashing. */
            private static unsafe uint HashBytes(byte* data) {
                ulong h = ((*(ulong*) (data) << (64 - 8 * HASH_LEN)) *
                           kHashMul64);
                /* The higher bits contain more mixture from the multiplication,
                   so we take our results from there. */
                return (uint) (h >> (64 - BUCKET_BITS));
            }

            /* A (forgetful) hash table to the data seen by the compressor, to
               help create backward references to previous data.

               This is a hash map of fixed size (BUCKET_SIZE). Starting from the
               given index, BUCKET_SWEEP buckets are used to store values of a key. */
            [StructLayout(LayoutKind.Sequential)]
            private unsafe struct HashLongestMatchQuickly {
                public fixed uint buckets_[BUCKET_SIZE + BUCKET_SWEEP];
            }

            private static unsafe HashLongestMatchQuickly* Self(HasherHandle handle) {
                return (HashLongestMatchQuickly*) &(GetHasherCommon(handle)[1]);
            }

            public override unsafe void Initialize(HasherHandle handle, BrotliEncoderParams* params_) {
            }

            public override unsafe void Prepare(HasherHandle handle, bool one_shot, SizeT input_size, byte* data) {
                HashLongestMatchQuickly* self = Self(handle);
                /* Partial preparation is 100 times slower (per socket). */
                size_t partial_prepare_threshold = HASH_MAP_SIZE >> 7;
                if (one_shot && input_size <= partial_prepare_threshold) {
                    size_t i;
                    for (i = 0; i < input_size; ++i) {
                        uint key = HashBytes(&data[i]);
                        memset(&self->buckets_[key], 0, BUCKET_SWEEP * sizeof(uint));
                    }
                }
                else {
                    /* It is not strictly necessary to fill this buffer here, but
                       not filling will make the results of the compression stochastic
                       (but correct). This is because random data would cause the
                       system to find accidentally good backward references here and there. */
                    memset(&self->buckets_[0], 0, sizeof(uint) * (BUCKET_SIZE + BUCKET_SWEEP));
                }
            }

            public override unsafe size_t HashMemAllocInBytes(BrotliEncoderParams* params_, bool one_shot,
                size_t input_size) {
                return Marshal.SizeOf(typeof(HashLongestMatchQuickly));
            }

            /* Look at 5 bytes at &data[ix & mask].
               Compute a hash from these, and store the value somewhere within
               [ix .. ix+3]. */
            public override unsafe void Store(HasherHandle handle,
                byte* data, size_t mask, size_t ix) {
                uint key = HashBytes(&data[ix & mask]);
                /* Wiggle the value with the bucket sweep range. */
                uint off = (ix >> 3) % BUCKET_SWEEP;
                Self(handle)->buckets_[key + off] = (uint) ix;
            }

            public override unsafe void StitchToPreviousBlock(HasherHandle handle, size_t num_bytes, size_t position,
                byte* ringbuffer,
                size_t ringbuffer_mask) {
                if (num_bytes >= HashTypeLength() - 1 && position >= 3) {
                    /* Prepare the hashes for three last bytes of the last write.
                       These could not be calculated before, since they require knowledge
                       of both the previous and the current block. */
                    Store(handle, ringbuffer, ringbuffer_mask, position - 3);
                    Store(handle, ringbuffer, ringbuffer_mask, position - 2);
                    Store(handle, ringbuffer, ringbuffer_mask, position - 1);
                }
            }
        }

        private class HashLongestMatchQuicklyH4 : Hasher {
            private const int BUCKET_BITS = 17;
            private const int BUCKET_SWEEP = 4;
            private const int HASH_LEN = 5;
            private const int USE_DICTIONARY = 1;
            private const int BUCKET_SIZE = 1 << BUCKET_BITS;
            private const int HASH_MAP_SIZE = 4 << BUCKET_BITS;

            public size_t HashTypeLength() {
                return 8;
            }

            public override size_t StoreLookahead() {
                return 8;
            }

            /* HashBytes is the function that chooses the bucket to place
               the address in. The HashLongestMatch and HashLongestMatchQuickly
               classes have separate, different implementations of hashing. */
            private static unsafe uint HashBytes(byte* data) {
                ulong h = ((*(ulong*) (data) << (64 - 8 * HASH_LEN)) *
                           kHashMul64);
                /* The higher bits contain more mixture from the multiplication,
                   so we take our results from there. */
                return (uint) (h >> (64 - BUCKET_BITS));
            }

            /* A (forgetful) hash table to the data seen by the compressor, to
               help create backward references to previous data.

               This is a hash map of fixed size (BUCKET_SIZE). Starting from the
               given index, BUCKET_SWEEP buckets are used to store values of a key. */
            [StructLayout(LayoutKind.Sequential)]
            private unsafe struct HashLongestMatchQuickly {
                public fixed uint buckets_[BUCKET_SIZE + BUCKET_SWEEP];
            }

            private static unsafe HashLongestMatchQuickly* Self(HasherHandle handle) {
                return (HashLongestMatchQuickly*) &(GetHasherCommon(handle)[1]);
            }

            public override unsafe void Initialize(HasherHandle handle, BrotliEncoderParams* params_) {
            }

            public override unsafe void Prepare(HasherHandle handle, bool one_shot, SizeT input_size, byte* data) {
                HashLongestMatchQuickly* self = Self(handle);
                /* Partial preparation is 100 times slower (per socket). */
                size_t partial_prepare_threshold = HASH_MAP_SIZE >> 7;
                if (one_shot && input_size <= partial_prepare_threshold) {
                    size_t i;
                    for (i = 0; i < input_size; ++i) {
                        uint key = HashBytes(&data[i]);
                        memset(&self->buckets_[key], 0, BUCKET_SWEEP * sizeof(uint));
                    }
                }
                else {
                    /* It is not strictly necessary to fill this buffer here, but
                       not filling will make the results of the compression stochastic
                       (but correct). This is because random data would cause the
                       system to find accidentally good backward references here and there. */
                    memset(&self->buckets_[0], 0, sizeof(uint) * (BUCKET_SIZE + BUCKET_SWEEP));
                }
            }

            public override unsafe size_t HashMemAllocInBytes(BrotliEncoderParams* params_, bool one_shot,
                size_t input_size) {
                return Marshal.SizeOf(typeof(HashLongestMatchQuickly));
            }

            /* Look at 5 bytes at &data[ix & mask].
               Compute a hash from these, and store the value somewhere within
               [ix .. ix+3]. */
            public override unsafe void Store(HasherHandle handle,
                byte* data, size_t mask, size_t ix) {
                uint key = HashBytes(&data[ix & mask]);
                /* Wiggle the value with the bucket sweep range. */
                uint off = (ix >> 3) % BUCKET_SWEEP;
                Self(handle)->buckets_[key + off] = (uint) ix;
            }

            public override unsafe void StitchToPreviousBlock(HasherHandle handle, size_t num_bytes, size_t position,
                byte* ringbuffer,
                size_t ringbuffer_mask) {
                if (num_bytes >= HashTypeLength() - 1 && position >= 3) {
                    /* Prepare the hashes for three last bytes of the last write.
                       These could not be calculated before, since they require knowledge
                       of both the previous and the current block. */
                    Store(handle, ringbuffer, ringbuffer_mask, position - 3);
                    Store(handle, ringbuffer, ringbuffer_mask, position - 2);
                    Store(handle, ringbuffer, ringbuffer_mask, position - 1);
                }
            }
        }

        private class HashLongestMatchQuicklyH54 : Hasher {
            private const int BUCKET_BITS = 20;
            private const int BUCKET_SWEEP = 4;
            private const int HASH_LEN = 7;
            private const int USE_DICTIONARY = 0;
            private const int BUCKET_SIZE = 1 << BUCKET_BITS;
            private const int HASH_MAP_SIZE = 4 << BUCKET_BITS;

            public size_t HashTypeLength() {
                return 8;
            }

            public override size_t StoreLookahead() {
                return 8;
            }

            /* HashBytes is the function that chooses the bucket to place
               the address in. The HashLongestMatch and HashLongestMatchQuickly
               classes have separate, different implementations of hashing. */
            private static unsafe uint HashBytes(byte* data) {
                ulong h = ((*(ulong*) (data) << (64 - 8 * HASH_LEN)) *
                           kHashMul64);
                /* The higher bits contain more mixture from the multiplication,
                   so we take our results from there. */
                return (uint) (h >> (64 - BUCKET_BITS));
            }

            /* A (forgetful) hash table to the data seen by the compressor, to
               help create backward references to previous data.

               This is a hash map of fixed size (BUCKET_SIZE). Starting from the
               given index, BUCKET_SWEEP buckets are used to store values of a key. */
            [StructLayout(LayoutKind.Sequential)]
            private unsafe struct HashLongestMatchQuickly {
                public fixed uint buckets_[BUCKET_SIZE + BUCKET_SWEEP];
            }

            private static unsafe HashLongestMatchQuickly* Self(HasherHandle handle) {
                return (HashLongestMatchQuickly*) &(GetHasherCommon(handle)[1]);
            }

            public override unsafe void Initialize(HasherHandle handle, BrotliEncoderParams* params_) {
            }

            public override unsafe void Prepare(HasherHandle handle, bool one_shot, SizeT input_size, byte* data) {
                HashLongestMatchQuickly* self = Self(handle);
                /* Partial preparation is 100 times slower (per socket). */
                size_t partial_prepare_threshold = HASH_MAP_SIZE >> 7;
                if (one_shot && input_size <= partial_prepare_threshold) {
                    size_t i;
                    for (i = 0; i < input_size; ++i) {
                        uint key = HashBytes(&data[i]);
                        memset(&self->buckets_[key], 0, BUCKET_SWEEP * sizeof(uint));
                    }
                }
                else {
                    /* It is not strictly necessary to fill this buffer here, but
                       not filling will make the results of the compression stochastic
                       (but correct). This is because random data would cause the
                       system to find accidentally good backward references here and there. */
                    memset(&self->buckets_[0], 0, sizeof(uint) * (BUCKET_SIZE + BUCKET_SWEEP));
                }
            }

            public override unsafe size_t HashMemAllocInBytes(BrotliEncoderParams* params_, bool one_shot,
                size_t input_size) {
                return Marshal.SizeOf(typeof(HashLongestMatchQuickly));
            }

            /* Look at 5 bytes at &data[ix & mask].
               Compute a hash from these, and store the value somewhere within
               [ix .. ix+3]. */
            public override unsafe void Store(HasherHandle handle,
                byte* data, size_t mask, size_t ix) {
                uint key = HashBytes(&data[ix & mask]);
                /* Wiggle the value with the bucket sweep range. */
                uint off = (ix >> 3) % BUCKET_SWEEP;
                Self(handle)->buckets_[key + off] = (uint) ix;
            }

            public override unsafe void StitchToPreviousBlock(HasherHandle handle, size_t num_bytes, size_t position,
                byte* ringbuffer,
                size_t ringbuffer_mask) {
                if (num_bytes >= HashTypeLength() - 1 && position >= 3) {
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