﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using size_t = BrotliSharpLib.Brotli.SizeT;

namespace BrotliSharpLib {
    /// <summary>
    /// A class for compressing and decompressing data using the brotli algorithm.
    /// </summary>
    public static partial class Brotli {
        /// <summary>
        /// Decompresses a byte array into a new byte array using brotli.
        /// </summary>
        /// <param name="buffer">The byte array to decompress.</param>
        /// <param name="offset">The byte offset in <paramref name="buffer"/> to read from.</param>
        /// <param name="length">The number of bytes to read.</param>
        /// <param name="customDictionary">The custom dictionary that will be passed to the decoder</param>
        /// <returns>The byte array in compressed form</returns>
        public static unsafe byte[] DecompressBuffer(byte[] buffer, int offset, int length, byte[] customDictionary = null) {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (offset + length > buffer.Length)
                throw new IndexOutOfRangeException("Offset and length exceed the range of the buffer");

            using (var ms = new MemoryStream()) {
                // Create the decoder state and intialise it.
                var s = BrotliCreateDecoderState();
                BrotliDecoderStateInit(ref s);

                // Set the custom dictionary
                GCHandle dictionaryHandle = customDictionary != null ? GCHandle.Alloc(customDictionary, GCHandleType.Pinned) : default(GCHandle);
                if (customDictionary != null)
                    BrotliDecoderSetCustomDictionary(ref s, customDictionary.Length, (byte*) dictionaryHandle.AddrOfPinnedObject());

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
                        if (customDictionary != null)
                            dictionaryHandle.Free();

                        if (result != BrotliDecoderResult.BROTLI_DECODER_RESULT_SUCCESS)
                            throw new InvalidDataException("Decompress failed with error code: " + s.error_code);

                        return ms.ToArray();
                    }
                }
            }
        }

#if HAS_SPAN
        /// <summary>
        /// Attempts to decompress the compressed data in <paramref name="buffer"/> into <paramref name="outBufferData"/>.
        /// </summary>
        /// <param name="buffer">The buffer containing compressed data.</param>
        /// <param name="outBufferData">The buffer to write the decompressed data to.</param>
        /// <param name="writtenData">The number of bytes written to the output buffer.</param>
        /// <param name="customDict">The custom dictionary that will be passed to the decoder.</param>
        /// <returns><see langword="true"/> if the input buffer was successfully decompressed, <see langword="false"/> otherwise.</returns>
        public static unsafe bool TryDecompressBuffer(ReadOnlySpan<byte> buffer, Span<byte> outBufferData, out int writtenData, ReadOnlySpan<byte> customDict = default)
        {
            // Create the decoder state and intialise it.
            var s = BrotliCreateDecoderState();
            BrotliDecoderStateInit(ref s);

            writtenData = 0;

            // Set the custom dictionary
            fixed (byte* customDictPtr = customDict)
            {
                if (customDictPtr != null)
                    BrotliDecoderSetCustomDictionary(ref s, customDict.Length, customDictPtr);

                // Pin the output buffer and the input buffer.
                fixed (byte* outBuffer = outBufferData)
                {
                    fixed (byte* inBuffer = buffer)
                    {
                        // Specify the length of the input buffer.
                        size_t len = buffer.Length;

                        // Local vars for input/output buffer.
                        var bufPtr = inBuffer;
                        var outPtr = outBuffer;

                        // Specify the amount of bytes available in the output buffer.
                        size_t availOut = outBufferData.Length;

                        // Total number of bytes decoded.
                        size_t total = 0;

                        // Main decompression loop.
                        var result = BrotliDecoderDecompressStream(ref s, &len, &bufPtr, &availOut, &outPtr, &total);

                        // Cleanup and throw.
                        BrotliDecoderStateCleanup(ref s);

                        if (result != BrotliDecoderResult.BROTLI_DECODER_RESULT_SUCCESS)
                            return false;

                        writtenData = outBufferData.Length - (int)availOut;
                        return true;
                    }
                }
            }
        }
#endif

        /// <summary>
        /// Compresses a byte array into a new byte array using brotli.
        /// </summary>
        /// <param name="buffer">The byte array to compress.</param>
        /// <param name="offset">The byte offset in <paramref name="buffer"/> to read from</param>.
        /// <param name="length">The number of bytes to read.</param>
        /// <param name="quality">The quality of the compression. This must be a value between 0 to 11 (inclusive).</param>
        /// <param name="lgwin">The window size (in bits). This must be a value between 10 and 24 (inclusive).</param>
        /// <param name="customDictionary">The custom dictionary that will be passed to the encoder.</param>
        /// <returns></returns>
        public static unsafe byte[] CompressBuffer(byte[] buffer, int offset, int length, int quality = -1,
            int lgwin = -1, byte[] customDictionary = null) {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (offset + length > buffer.Length)
                throw new IndexOutOfRangeException("Offset and length exceed the range of the buffer");

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

#if HAS_SPAN
        /// <summary>
        /// Tries to compress <paramref name="buffer"/> into <paramref name="outBuffer"/>.
        /// </summary>
        /// <param name="buffer">The buffer to read uncompressed data from.</param>
        /// <param name="outBuffer">The buffer to write compressed data to.</param>
        /// <param name="writtenData">The amount of data written to <paramref name="outBuffer"/>.</param>
        /// <param name="customDict">The custom dictionary that will be passed to the encoder.</param>
        /// <returns><see langword="true"/> if the input buffer was successfully compressed, <see langword="false"/> otherwise.</returns>
        public static bool TryCompressBuffer(ReadOnlySpan<byte> buffer, Span<byte> outBuffer, out int writtenData, ReadOnlySpan<byte> customDict = default)
            => TryCompressBuffer(buffer, outBuffer, out writtenData, customDict);
        /// <summary>
        /// Tries to compress <paramref name="buffer"/> into <paramref name="outBuffer"/>.
        /// </summary>
        /// <param name="buffer">The buffer to read uncompressed data from.</param>
        /// <param name="outBuffer">The buffer to write compressed data to.</param>
        /// <param name="writtenData">The amount of data written to <paramref name="outBuffer"/>.</param>
        /// <param name="quality">The quality of the compression. This must be a value between 0 to 11 (inclusive).</param>
        /// <param name="lgwin">The window size (in bits). This must be a value between 10 and 24 (inclusive).</param>
        /// <param name="customDict">The custom dictionary that will be passed to the encoder.</param>
        /// <returns><see langword="true"/> if the input buffer was successfully compressed, <see langword="false"/> otherwise.</returns>
        public static unsafe bool TryCompressBuffer(ReadOnlySpan<byte> buffer, Span<byte> outBuffer, out int writtenData,
            int quality, int lgwin, ReadOnlySpan<byte> customDict = default)
        {
            // Create the encoder state and intialise it.
            var s = BrotliEncoderCreateInstance(null, null, null);

            // Set the encoder parameters
            if (quality != -1)
                BrotliEncoderSetParameter(ref s, BrotliEncoderParameter.BROTLI_PARAM_QUALITY, (uint)quality);

            if (lgwin != -1)
                BrotliEncoderSetParameter(ref s, BrotliEncoderParameter.BROTLI_PARAM_LGWIN, (uint)lgwin);

            // Set the custom dictionary
            fixed (byte* cd = customDict)
            {
                if (cd != null)
                    BrotliEncoderSetCustomDictionary(ref s, customDict.Length, cd);
            }

            writtenData = 0;

            size_t available_in = buffer.Length;
            size_t available_out = outBuffer.Length;
            fixed (byte* o = outBuffer)
            fixed (byte* b = buffer)
            {
                byte* next_in = b;
                byte* next_out = o;

                bool fail = false;

                // Compress stream using inputted buffer
                if (!BrotliEncoderCompressStream(ref s, BrotliEncoderOperation.BROTLI_OPERATION_FINISH,
                    &available_in, &next_in, &available_out, &next_out, null))
                {
                    fail = true;
                }
                else
                {
                    // Check that the encoder is finished
                    if (BrotliEncoderIsFinished(ref s))
                    {
                        fail = true;
                    }
                }

                BrotliEncoderDestroyInstance(ref s);

                if (fail)
                    return false;
            }

            writtenData = outBuffer.Length - (int)available_out;
            return true;
        }
#endif
    }
}