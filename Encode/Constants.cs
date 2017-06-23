using System;
using System.Runtime.InteropServices;
using reg_t = BrotliSharpLib.Brotli.SizeT;

namespace BrotliSharpLib
{
    public static partial class Brotli {
        private const int BROTLI_MIN_WINDOW_BITS = 10;
        private const int BROTLI_MAX_WINDOW_BITS = 24;
        private const int BROTLI_MIN_INPUT_BLOCK_BITS = 16;
        private const int BROTLI_MAX_INPUT_BLOCK_BITS = 24;
        private const int BROTLI_MIN_QUALITY = 0;
        private const int BROTLI_MAX_QUALITY = 11;

        private const int FAST_ONE_PASS_COMPRESSION_QUALITY = 0;
        private const int FAST_TWO_PASS_COMPRESSION_QUALITY = 1;
        private const int MAX_QUALITY_FOR_STATIC_ENTROPY_CODES = 2;
        private const int MIN_QUALITY_FOR_BLOCK_SPLIT = 4;
        private const int MIN_QUALITY_FOR_OPTIMIZE_HISTOGRAMS = 4;
        private const int MIN_QUALITY_FOR_EXTENSIVE_REFERENCE_SEARCH = 5;
        private const int MIN_QUALITY_FOR_CONTEXT_MODELING = 5;
        private const int MIN_QUALITY_FOR_HQ_CONTEXT_MODELING = 7;
        private const int MIN_QUALITY_FOR_HQ_BLOCK_SPLITTING = 10;
        /* Only for "font" mode. */
        private const int MIN_QUALITY_FOR_RECOMPUTE_DISTANCE_PREFIXES = 10;

        private const uint kHashMul32 = 0x1e35a7bd;
        private const ulong kHashMul64 = 0x1e35a7bd1e35a7bd;
        private const ulong kHashMul64Long = 0x1fe35a7bd3579bd3;

        private const int BROTLI_DEFAULT_QUALITY = 11;
        private const int BROTLI_DEFAULT_WINDOW = 22;
        private const BrotliEncoderMode BROTLI_DEFAULT_MODE = BrotliEncoderMode.BROTLI_MODE_GENERIC;
    }
}