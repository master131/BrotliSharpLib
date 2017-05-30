using System.IO;
using size_t = BrotliSharpLib.Brotli.SizeT;

namespace BrotliSharpLib {
    public static partial class Brotli {
        public static unsafe byte[] DecompressBuffer(byte[] buffer, int offset, int length) {
            using (var ms = new MemoryStream()) {
                // Create the decoder state and intialise it.
                var s = BrotliCreateDecoderState();
                BrotliDecoderStateInit(ref s);

                // Create a 4k buffer to temporarily store decompressed contents.
                byte[] writeBuf = new byte[0x1000];

                // Pin the output buffer and the input buffer.
                fixed (byte* outBuffer = writeBuf) {
                    fixed (byte* inBuffer = buffer) {
                        // Specify the length of the input buffer.
                        size_t len = length;

                        // Local vars for input/output buffer.
                        var bufPtr = inBuffer + offset;
                        var outPtr = outBuffer;

                        // Specify the amount of bytes available in the output buffer.
                        size_t availOut = writeBuf.Length;

                        // Total number of bytes decoded.
                        size_t total = 0;

                        // Main decompression loop.
                        BrotliDecoderResult result;
                        while (
                            (result =
                                BrotliDecoderDecompressStream(ref s, &len, &bufPtr, &availOut, &outPtr, &total)) ==
                            BrotliDecoderResult.BROTLI_DECODER_RESULT_NEEDS_MORE_OUTPUT) {
                            ms.Write(writeBuf, 0, (int) (writeBuf.Length - availOut));
                            availOut = writeBuf.Length;
                            outPtr = outBuffer;
                        }

                        // Check the result and write final block.
                        if (result == BrotliDecoderResult.BROTLI_DECODER_RESULT_SUCCESS)
                            ms.Write(writeBuf, 0, (int) (writeBuf.Length - availOut));

                        // Cleanup and throw.
                        BrotliDecoderStateCleanup(ref s);
                        if (result != BrotliDecoderResult.BROTLI_DECODER_RESULT_SUCCESS)
                            throw new InvalidDataException("Decompress failed with error code: " + s.error_code);

                        return ms.ToArray();
                    }
                }
            }
        }
    }
}