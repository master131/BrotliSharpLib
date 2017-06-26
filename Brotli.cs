using System.IO;
using size_t = BrotliSharpLib.Brotli.SizeT;

namespace BrotliSharpLib {
    public static partial class Brotli {
        public static unsafe byte[] DecompressBuffer(byte[] buffer, int offset, int length) {
            using (var ms = new MemoryStream()) {
                // Create the decoder state and intialise it.
                var s = BrotliCreateDecoderState();
                BrotliDecoderStateInit(ref s);

                // Create a 64k buffer to temporarily store decompressed contents.
                byte[] writeBuf = new byte[0x10000];

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

        public static unsafe byte[] CompressBuffer(byte[] buffer, int offset, int length, int quality = -1,
            int lgwin = -1, byte[] customDictionary = null) {
            using (var ms = new MemoryStream()) {
                // Create the encoder state and intialise it.
                var s = BrotliEncoderCreateInstance(null, null, null);

                // Set the encoder parameters
                if (quality != -1)
                    BrotliEncoderSetParameter(ref s, BrotliEncoderParameter.BROTLI_PARAM_QUALITY, (uint) quality);

                if (lgwin != -1)
                    BrotliEncoderSetParameter(ref s, BrotliEncoderParameter.BROTLI_PARAM_LGWIN, (uint) lgwin);

                // Set the custom dictionary
                if (customDictionary != null) {
                    fixed (byte* cd = customDictionary)
                        BrotliEncoderSetCustomDictionary(ref s, customDictionary.Length, cd);
                }

                size_t available_in = length;
                byte[] out_buf = new byte[0x10000];
                size_t available_out = out_buf.Length;
                fixed (byte* o = out_buf)
                fixed (byte* b = buffer) {
                    byte* next_in = b;
                    byte* next_out = o;

                    bool fail = false;

                    while (true) {
                        // Compress stream using inputted buffer
                        if (!BrotliEncoderCompressStream(ref s, BrotliEncoderOperation.BROTLI_OPERATION_FINISH,
                            &available_in, &next_in, &available_out, &next_out, null)) {
                            fail = true;
                            break;
                        }

                        // Write the compressed bytes to the stream
                        if (available_out != out_buf.Length) {
                            size_t out_size = out_buf.Length - available_out;
                            ms.Write(out_buf, 0, (int) out_size);
                            available_out = out_buf.Length;
                            next_out = o;
                        }

                        // Check that the encoder is finished
                        if (BrotliEncoderIsFinished(ref s)) break;
                    }

                    BrotliEncoderDestroyInstance(ref s);

                    if (fail)
                        throw new InvalidDataException("Compression failed for unspecified reason");
                }

                return ms.ToArray();
            }
        }
    }
}