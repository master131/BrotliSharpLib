using System;
using System.Runtime.InteropServices;
using size_t = BrotliSharpLib.Brotli.SizeT;

namespace BrotliSharpLib {
    public static partial class Brotli {
        private class HashForgetfulChainH40 : Hasher {
            private const int BUCKET_BITS = 15;
            private const int NUM_LAST_DISTANCES_TO_CHECK = 1;
            private const int NUM_BANKS = 1;
            private const int BANK_BITS = 16;
            private const int BANK_SIZE = 1 << BANK_BITS;
            private const int BUCKET_SIZE = 1 << BUCKET_BITS;
            private const bool CAPPED_CHAINS = false;

            public size_t HashTypeLength() {
                return 4;
            }

            public override size_t StoreLookahead() {
                return 4;
            }

            /* HashBytes is the function that chooses the bucket to place the address in. */
            private static unsafe uint HashBytes(byte* data) {
                uint h = *(uint*) (data) * kHashMul32;
                /* The higher bits contain more mixture from the multiplication,
                   so we take our results from there. */
                return h >> (32 - BUCKET_BITS);
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct Slot {
                public ushort delta;
                public ushort next;
            }

            [StructLayout(LayoutKind.Sequential)]
            private unsafe struct Bank {
                private fixed ushort slots_[BANK_SIZE * 2];

                public Slot* slots(size_t index) {
                    fixed (ushort* s = slots_)
                        return (Slot*) &s[index * 2];
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            private unsafe struct HashForgetfulChain {
                public fixed uint addr[BUCKET_SIZE];

                public fixed ushort head[BUCKET_SIZE];

                /* Truncated hash used for quick rejection of "distance cache" candidates. */
                public fixed byte tiny_hash[65536];

                private fixed ushort banks_[NUM_BANKS * (BANK_SIZE * 2)];
                public fixed ushort free_slot_idx[NUM_BANKS];
                public size_t max_hops;

                public Bank* banks(size_t index) {
                    fixed (ushort* s = banks_)
                        return (Bank*) &s[index * BANK_SIZE * 2];
                }
            }

            private static unsafe HashForgetfulChain* Self(HasherHandle handle) {
                return (HashForgetfulChain*) &(GetHasherCommon(handle)[1]);
            }

            public override unsafe void Initialize(HasherHandle handle, BrotliEncoderParams* params_) {
                Self(handle)->max_hops =
                    (params_->quality > 6 ? 7u : 8u) << (params_->quality - 4);
            }

            public override unsafe void Prepare(HasherHandle handle, bool one_shot, SizeT input_size, byte* data) {
                HashForgetfulChain* self = Self(handle);
                /* Partial preparation is 100 times slower (per socket). */
                size_t partial_prepare_threshold = BUCKET_SIZE >> 6;
                if (one_shot && input_size <= partial_prepare_threshold) {
                    size_t i;
                    for (i = 0; i < input_size; ++i) {
                        size_t bucket = HashBytes(&data[i]);
                        /* See InitEmpty comment. */
                        self->addr[bucket] = 0xCCCCCCCC;
                        self->head[bucket] = 0xCCCC;
                    }
                }
                else {
                    /* Fill |addr| array with 0xCCCCCCCC value. Because of wrapping, position
                       processed by hasher never reaches 3GB + 64M; this makes all new chains
                       to be terminated after the first node. */
                    memset(self->addr, 0xCC, BUCKET_SIZE * sizeof(uint));
                    memset(self->head, 0, BUCKET_SIZE * sizeof(ushort));
                }
                memset(self->tiny_hash, 0, 65536);
                memset(self->free_slot_idx, 0, NUM_BANKS * sizeof(ushort));
            }

            public override unsafe size_t HashMemAllocInBytes(BrotliEncoderParams* params_, bool one_shot,
                size_t input_size) {
                return Marshal.SizeOf(typeof(HashForgetfulChain));
            }

            /* Look at 4 bytes at &data[ix & mask]. Compute a hash from these, and prepend
               node to corresponding chain; also update tiny_hash for current position. */
            public override unsafe void Store(HasherHandle handle,
                byte* data, size_t mask, size_t ix) {
                HashForgetfulChain* self = Self(handle);
                size_t key = HashBytes(&data[ix & mask]);
                size_t bank = key & (NUM_BANKS - 1);
                size_t idx = self->free_slot_idx[bank]++ & (BANK_SIZE - 1);
                size_t delta = ix - self->addr[key];
                self->tiny_hash[(ushort) ix] = (byte) key;
                if (delta > 0xFFFF) delta = CAPPED_CHAINS ? 0 : 0xFFFF;
                self->banks(bank)->slots(idx)->delta = (ushort) delta;
                self->banks(bank)->slots(idx)->next = self->head[key];
                self->addr[key] = (uint) ix;
                self->head[key] = (ushort) idx;
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

        private class HashForgetfulChainH41 : Hasher {
            private const int BUCKET_BITS = 15;
            private const int NUM_LAST_DISTANCES_TO_CHECK = 10;
            private const int NUM_BANKS = 1;
            private const int BANK_BITS = 16;
            private const int BANK_SIZE = 1 << BANK_BITS;
            private const int BUCKET_SIZE = 1 << BUCKET_BITS;
            private const bool CAPPED_CHAINS = false;

            public size_t HashTypeLength() {
                return 4;
            }

            public override size_t StoreLookahead() {
                return 4;
            }

            /* HashBytes is the function that chooses the bucket to place the address in. */
            private static unsafe uint HashBytes(byte* data) {
                uint h = *(uint*) (data) * kHashMul32;
                /* The higher bits contain more mixture from the multiplication,
                   so we take our results from there. */
                return h >> (32 - BUCKET_BITS);
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct Slot {
                public ushort delta;
                public ushort next;
            }

            [StructLayout(LayoutKind.Sequential)]
            private unsafe struct Bank {
                private fixed ushort slots_[BANK_SIZE * 2];

                public Slot* slots(size_t index) {
                    fixed (ushort* s = slots_)
                        return (Slot*) &s[index * 2];
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            private unsafe struct HashForgetfulChain {
                public fixed uint addr[BUCKET_SIZE];

                public fixed ushort head[BUCKET_SIZE];

                /* Truncated hash used for quick rejection of "distance cache" candidates. */
                public fixed byte tiny_hash[65536];

                private fixed ushort banks_[NUM_BANKS * (BANK_SIZE * 2)];
                public fixed ushort free_slot_idx[NUM_BANKS];
                public size_t max_hops;

                public Bank* banks(size_t index) {
                    fixed (ushort* s = banks_)
                        return (Bank*) &s[index * BANK_SIZE * 2];
                }
            }

            private static unsafe HashForgetfulChain* Self(HasherHandle handle) {
                return (HashForgetfulChain*) &(GetHasherCommon(handle)[1]);
            }

            public override unsafe void Initialize(HasherHandle handle, BrotliEncoderParams* params_) {
                Self(handle)->max_hops =
                    (params_->quality > 6 ? 7u : 8u) << (params_->quality - 4);
            }

            public override unsafe void Prepare(HasherHandle handle, bool one_shot, SizeT input_size, byte* data) {
                HashForgetfulChain* self = Self(handle);
                /* Partial preparation is 100 times slower (per socket). */
                size_t partial_prepare_threshold = BUCKET_SIZE >> 6;
                if (one_shot && input_size <= partial_prepare_threshold) {
                    size_t i;
                    for (i = 0; i < input_size; ++i) {
                        size_t bucket = HashBytes(&data[i]);
                        /* See InitEmpty comment. */
                        self->addr[bucket] = 0xCCCCCCCC;
                        self->head[bucket] = 0xCCCC;
                    }
                }
                else {
                    /* Fill |addr| array with 0xCCCCCCCC value. Because of wrapping, position
                       processed by hasher never reaches 3GB + 64M; this makes all new chains
                       to be terminated after the first node. */
                    memset(self->addr, 0xCC, BUCKET_SIZE * sizeof(uint));
                    memset(self->head, 0, BUCKET_SIZE * sizeof(ushort));
                }
                memset(self->tiny_hash, 0, 65536);
                memset(self->free_slot_idx, 0, NUM_BANKS * sizeof(ushort));
            }

            public override unsafe size_t HashMemAllocInBytes(BrotliEncoderParams* params_, bool one_shot,
                size_t input_size) {
                return Marshal.SizeOf(typeof(HashForgetfulChain));
            }

            /* Look at 4 bytes at &data[ix & mask]. Compute a hash from these, and prepend
               node to corresponding chain; also update tiny_hash for current position. */
            public override unsafe void Store(HasherHandle handle,
                byte* data, size_t mask, size_t ix) {
                HashForgetfulChain* self = Self(handle);
                size_t key = HashBytes(&data[ix & mask]);
                size_t bank = key & (NUM_BANKS - 1);
                size_t idx = self->free_slot_idx[bank]++ & (BANK_SIZE - 1);
                size_t delta = ix - self->addr[key];
                self->tiny_hash[(ushort) ix] = (byte) key;
                if (delta > 0xFFFF) delta = CAPPED_CHAINS ? 0 : 0xFFFF;
                self->banks(bank)->slots(idx)->delta = (ushort) delta;
                self->banks(bank)->slots(idx)->next = self->head[key];
                self->addr[key] = (uint) ix;
                self->head[key] = (ushort) idx;
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

        private class HashForgetfulChainH42 : Hasher {
            private const int BUCKET_BITS = 15;
            private const int NUM_LAST_DISTANCES_TO_CHECK = 16;
            private const int NUM_BANKS = 512;
            private const int BANK_BITS = 9;
            private const int BANK_SIZE = 1 << BANK_BITS;
            private const int BUCKET_SIZE = 1 << BUCKET_BITS;
            private const bool CAPPED_CHAINS = false;

            public size_t HashTypeLength() {
                return 4;
            }

            public override size_t StoreLookahead() {
                return 4;
            }

            /* HashBytes is the function that chooses the bucket to place the address in. */
            private static unsafe uint HashBytes(byte* data) {
                uint h = *(uint*) (data) * kHashMul32;
                /* The higher bits contain more mixture from the multiplication,
                   so we take our results from there. */
                return h >> (32 - BUCKET_BITS);
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct Slot {
                public ushort delta;
                public ushort next;
            }

            [StructLayout(LayoutKind.Sequential)]
            private unsafe struct Bank {
                private fixed ushort slots_[BANK_SIZE * 2];

                public Slot* slots(size_t index) {
                    fixed (ushort* s = slots_)
                        return (Slot*) &s[index * 2];
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            private unsafe struct HashForgetfulChain {
                public fixed uint addr[BUCKET_SIZE];

                public fixed ushort head[BUCKET_SIZE];

                /* Truncated hash used for quick rejection of "distance cache" candidates. */
                public fixed byte tiny_hash[65536];

                private fixed ushort banks_[NUM_BANKS * (BANK_SIZE * 2)];
                public fixed ushort free_slot_idx[NUM_BANKS];
                public size_t max_hops;

                public Bank* banks(size_t index) {
                    fixed (ushort* s = banks_)
                        return (Bank*) &s[index * BANK_SIZE * 2];
                }
            }

            private static unsafe HashForgetfulChain* Self(HasherHandle handle) {
                return (HashForgetfulChain*) &(GetHasherCommon(handle)[1]);
            }

            public override unsafe void Initialize(HasherHandle handle, BrotliEncoderParams* params_) {
                Self(handle)->max_hops =
                    (params_->quality > 6 ? 7u : 8u) << (params_->quality - 4);
            }

            public override unsafe void Prepare(HasherHandle handle, bool one_shot, SizeT input_size, byte* data) {
                HashForgetfulChain* self = Self(handle);
                /* Partial preparation is 100 times slower (per socket). */
                size_t partial_prepare_threshold = BUCKET_SIZE >> 6;
                if (one_shot && input_size <= partial_prepare_threshold) {
                    size_t i;
                    for (i = 0; i < input_size; ++i) {
                        size_t bucket = HashBytes(&data[i]);
                        /* See InitEmpty comment. */
                        self->addr[bucket] = 0xCCCCCCCC;
                        self->head[bucket] = 0xCCCC;
                    }
                }
                else {
                    /* Fill |addr| array with 0xCCCCCCCC value. Because of wrapping, position
                       processed by hasher never reaches 3GB + 64M; this makes all new chains
                       to be terminated after the first node. */
                    memset(self->addr, 0xCC, BUCKET_SIZE * sizeof(uint));
                    memset(self->head, 0, BUCKET_SIZE * sizeof(ushort));
                }
                memset(self->tiny_hash, 0, 65536);
                memset(self->free_slot_idx, 0, NUM_BANKS * sizeof(ushort));
            }

            public override unsafe size_t HashMemAllocInBytes(BrotliEncoderParams* params_, bool one_shot,
                size_t input_size) {
                return Marshal.SizeOf(typeof(HashForgetfulChain));
            }

            /* Look at 4 bytes at &data[ix & mask]. Compute a hash from these, and prepend
               node to corresponding chain; also update tiny_hash for current position. */
            public override unsafe void Store(HasherHandle handle,
                byte* data, size_t mask, size_t ix) {
                HashForgetfulChain* self = Self(handle);
                size_t key = HashBytes(&data[ix & mask]);
                size_t bank = key & (NUM_BANKS - 1);
                size_t idx = self->free_slot_idx[bank]++ & (BANK_SIZE - 1);
                size_t delta = ix - self->addr[key];
                self->tiny_hash[(ushort) ix] = (byte) key;
                if (delta > 0xFFFF) delta = CAPPED_CHAINS ? 0 : 0xFFFF;
                self->banks(bank)->slots(idx)->delta = (ushort) delta;
                self->banks(bank)->slots(idx)->next = self->head[key];
                self->addr[key] = (uint) ix;
                self->head[key] = (ushort) idx;
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