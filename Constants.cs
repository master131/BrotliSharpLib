using System.Runtime.InteropServices;
using reg_t = BrotliSharpLib.Brotli.SizeT;

namespace BrotliSharpLib {
    public static partial class Brotli {
        private const int BROTLI_HUFFMAN_MAX_CODE_LENGTH = 15;
        private const int BROTLI_NUM_COMMAND_SYMBOLS = 704;
        private const int BROTLI_REPEAT_ZERO_CODE_LENGTH = 17;
        private const int BROTLI_CODE_LENGTH_CODES = BROTLI_REPEAT_ZERO_CODE_LENGTH + 1;
        private const int BROTLI_HUFFMAN_MAX_SIZE_26 = 396;
        private const int BROTLI_HUFFMAN_MAX_SIZE_258 = 632;
        private const int BROTLI_HUFFMAN_MAX_SIZE_272 = 646;
        private const int BROTLI_WINDOW_GAP = 16;
        private const int BROTLI_HUFFMAN_MAX_CODE_LENGTH_CODE_LENGTH = 5;
        private const int BROTLI_REVERSE_BITS_MAX = 8;
        private const int BROTLI_REVERSE_BITS_BASE = 0;
        private const int BROTLI_INITIAL_REPEATED_CODE_LENGTH = 8;
        private const int BROTLI_REPEAT_PREVIOUS_CODE_LENGTH = 16;
        private const int BROTLI_NUM_BLOCK_LEN_SYMBOLS = 26;
        private const int BROTLI_NUM_DISTANCE_SHORT_CODES = 16;
        private const int BROTLI_LITERAL_CONTEXT_BITS = 6;
        private const int HUFFMAN_TABLE_MASK = 0xff;
        private const int BROTLI_MAX_DISTANCE_BITS = 24;
        private const int BROTLI_DISTANCE_CONTEXT_BITS = 2;
        private const int BROTLI_NUM_LITERAL_SYMBOLS = 256;
        private const int BROTLI_MIN_DICTIONARY_WORD_LENGTH = 4;
        private const int BROTLI_MAX_DICTIONARY_WORD_LENGTH = 24;

        private const int HUFFMAN_TABLE_BITS = 8;

        private const bool BROTLI_ALIGNED_READ = false;

        private static readonly reg_t BROTLI_REVERSE_BITS_LOWEST = (reg_t) 1 <<
                                                                   (BROTLI_REVERSE_BITS_MAX - 1 +
                                                                    BROTLI_REVERSE_BITS_BASE);

        private static readonly int BROTLI_SHORT_FILL_BIT_WINDOW_READ = Marshal.SizeOf(typeof(reg_t)) >> 1;
    }
}