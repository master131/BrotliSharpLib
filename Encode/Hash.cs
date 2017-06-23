using System.Collections.Generic;
using System.Runtime.InteropServices;
using size_t = BrotliSharpLib.Brotli.SizeT;

namespace BrotliSharpLib {
    public static partial class Brotli {
        private static readonly Dictionary<int, Hasher> kHashers =
            new Dictionary<int, Hasher> {
                {10, new HashToBinaryTreeH10()},
                {2, new HashLongestMatchQuicklyH2()},
                {3, new HashLongestMatchQuicklyH3()},
                {4, new HashLongestMatchQuicklyH4()},
                {5, new HashLongestMatchH5()},
                {6, new HashLongestMatch64H6()},
                {40, new HashForgetfulChainH40()},
                {41, new HashForgetfulChainH41()},
                {42, new HashForgetfulChainH42()},
                {54, new HashLongestMatchQuicklyH54()}
            };

        [StructLayout(LayoutKind.Sequential)]
        private struct HasherCommon {
            public BrotliHasherParams params_;

            /* False if hasher needs to be "prepared" before use. */
            public bool is_prepared_;

            public size_t dict_num_lookups;
            public size_t dict_num_matches;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BackwardMatch {
            public uint distance;
            public uint length_and_code;
        }

        private static unsafe void InitBackwardMatch(BackwardMatch* self,
            size_t dist, size_t len) {
            self->distance = (uint) dist;
            self->length_and_code = (uint) (len << 5);
        }

        private static unsafe HasherCommon* GetHasherCommon(HasherHandle handle) {
            return (HasherCommon*) handle;
        }

        private static unsafe void HasherReset(HasherHandle handle)
        {
            if ((void*) handle == null) return;
            GetHasherCommon(handle)->is_prepared_ = false;
        }

        private static unsafe size_t HasherSize(BrotliEncoderParams* params_,
            bool one_shot, size_t input_size) {
            size_t result = sizeof(HasherCommon);
            return result + kHashers[params_->hasher.type].HashMemAllocInBytes(params_, one_shot, input_size);
        }

        private static unsafe void HasherSetup(ref MemoryManager m, HasherHandle* handle,
            BrotliEncoderParams* params_, byte* data, size_t position,
            size_t input_size, bool is_last) {
            HasherHandle self = null;
            HasherCommon* common = null;
            bool one_shot = (position == 0 && is_last);
            if ((byte*) (*handle) == null) {
                size_t alloc_size;
                ChooseHasher(params_, &params_->hasher);
                alloc_size = HasherSize(params_, one_shot, input_size);
                self = BrotliAllocate(ref m, alloc_size);
                *handle = self;
                common = GetHasherCommon(self);
                common->params_ = params_->hasher;
                kHashers[common->params_.type].Initialize(*handle, params_);
                HasherReset(*handle);
            }

            self = *handle;
            common = GetHasherCommon(self);
            if (!common->is_prepared_) {
                kHashers[common->params_.type].Prepare(self, one_shot, input_size, data);
                if (position == 0) {
                    common->dict_num_lookups = 0;
                    common->dict_num_matches = 0;
                }
                common->is_prepared_ = true;
            }
        }

        /* Custom LZ77 window. */
        private static unsafe void HasherPrependCustomDictionary(
            ref MemoryManager m, HasherHandle* handle, BrotliEncoderParams* params_,
            size_t size, byte* dict) {
            size_t overlap;
            size_t i;
            HasherHandle self;
            HasherSetup(ref m, handle, params_, dict, 0, size, false);
            self = *handle;
            Hasher h = kHashers[GetHasherCommon(self)->params_.type];
            overlap = h.StoreLookahead() - 1;
            for (i = 0; i + overlap < size; i++)
                h.Store(self, dict, ~(size_t) 0, i);
        }
    }
}