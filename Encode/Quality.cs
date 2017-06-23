using System;

namespace BrotliSharpLib
{
    public static partial class Brotli
    {
        private static unsafe void SanitizeParams(BrotliEncoderParams* params_)
        {
            params_->quality = Math.Min(BROTLI_MAX_QUALITY,
                Math.Max(BROTLI_MIN_QUALITY, params_->quality));
            if (params_->lgwin < BROTLI_MIN_WINDOW_BITS) {
                params_->lgwin = BROTLI_MIN_WINDOW_BITS;
            } else if (params_->lgwin > BROTLI_MAX_WINDOW_BITS) {
                params_->lgwin = BROTLI_MAX_WINDOW_BITS;
            }
        }

        /* Returns optimized lg_block value. */
        private static unsafe int ComputeLgBlock(BrotliEncoderParams* params_) {
            int lgblock = params_->lgblock;
            if (params_->quality == FAST_ONE_PASS_COMPRESSION_QUALITY ||
                params_->quality == FAST_TWO_PASS_COMPRESSION_QUALITY) {
                lgblock = params_->lgwin;
            } else if (params_->quality < MIN_QUALITY_FOR_BLOCK_SPLIT) {
                lgblock = 14;
            } else if (lgblock == 0) {
                lgblock = 16;
                if (params_->quality >= 9 && params_->lgwin > lgblock) {
                    lgblock = Math.Min(18, params_->lgwin);
                }
            } else {
                lgblock = Math.Min(BROTLI_MAX_INPUT_BLOCK_BITS,
                    Math.Max(BROTLI_MIN_INPUT_BLOCK_BITS, lgblock));
            }
            return lgblock;
        }

        /* Returns log2 of the size of main ring buffer area.
           Allocate at least lgwin + 1 bits for the ring buffer so that the newly
           added block fits there completely and we still get lgwin bits and at least
           read_block_size_bits + 1 bits because the copy tail length needs to be
           smaller than ring-buffer size. */
        private static unsafe int ComputeRbBits(BrotliEncoderParams* params_) {
            return 1 + Math.Max(params_->lgwin, params_->lgblock);
        }

        static unsafe void ChooseHasher(BrotliEncoderParams* params_,
            BrotliHasherParams* hparams) {
            if (params_->quality > 9) {
                hparams->type = 10;
            } else if (params_->quality == 4 && params_->size_hint >= (1 << 20)) {
                hparams->type = 54;
            } else if (params_->quality< 5) {
                hparams->type = params_->quality;
            } else if (params_->lgwin <= 16) {
                hparams->type = params_->quality< 7 ? 40 : params_->quality< 9 ? 41 : 42;
            } else if (params_->size_hint >= (1 << 20) && params_->lgwin >= 19) {
                hparams->type = 6;
                hparams->block_bits = params_->quality - 1;
                hparams->bucket_bits = 15;
                hparams->hash_len = 5;
                hparams->num_last_distances_to_check =
                    params_->quality< 7 ? 4 : params_->quality< 9 ? 10 : 16;
            } else {
                hparams->type = 5;
                hparams->block_bits = params_->quality - 1;
                hparams->bucket_bits = params_->quality< 7 ? 14 : 15;
                hparams->num_last_distances_to_check =
                    params_->quality< 7 ? 4 : params_->quality< 9 ? 10 : 16;
            }
        }
    }
}