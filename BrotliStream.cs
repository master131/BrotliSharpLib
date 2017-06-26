using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using size_t = BrotliSharpLib.Brotli.SizeT;

namespace BrotliSharpLib
{
    public unsafe class BrotliStream : Stream {
        private Stream _stream;
        private CompressionMode _mode;
        private bool _leaveOpen, _disposed;
        private IntPtr _customDictionary = IntPtr.Zero;

        private Brotli.BrotliEncoderStateStruct _encoderState;
        private Brotli.BrotliDecoderStateStruct _decoderState;

        private Brotli.BrotliDecoderResult _lastDecoderState =
            Brotli.BrotliDecoderResult.BROTLI_DECODER_RESULT_NEEDS_MORE_INPUT;

        public BrotliStream(Stream stream, CompressionMode mode, bool leaveOpen) {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (CompressionMode.Compress != mode && CompressionMode.Decompress != mode)
                throw new ArgumentOutOfRangeException(nameof(mode));

            _stream = stream;
            _mode = mode;
            _leaveOpen = leaveOpen;

            switch (_mode) {
                case CompressionMode.Decompress:
                    if (!_stream.CanRead)
                        throw new ArgumentException("Stream does not support read", nameof(stream));

                    _decoderState = Brotli.BrotliCreateDecoderState();
                    Brotli.BrotliDecoderStateInit(ref _decoderState);
                    break;
                case CompressionMode.Compress:
                    if (!_stream.CanWrite)
                        throw new ArgumentException("Stream does not support write", nameof(stream));

                    _encoderState = Brotli.BrotliEncoderCreateInstance(null, null, null);
                    break;
            }
        }

        public BrotliStream(Stream stream, CompressionMode mode) :
            this(stream, mode, false) {
        }

        /// <summary>
        /// Sets the quality for compression
        /// </summary>
        /// <param name="quality">A value from 0-11</param>
        public void SetQuality(int quality) {
            if (_mode != CompressionMode.Compress)
                throw new InvalidOperationException("SetQuality is only valid for compress");

            if (quality < Brotli.BROTLI_MIN_QUALITY || quality > Brotli.BROTLI_MAX_QUALITY)
                throw new ArgumentOutOfRangeException(nameof(quality), "Quality should be a value between " +
                                                                       Brotli.BROTLI_MIN_QUALITY + "-" + Brotli
                                                                           .BROTLI_MAX_QUALITY);

            Brotli.BrotliEncoderSetParameter(ref _encoderState, Brotli.BrotliEncoderParameter.BROTLI_PARAM_QUALITY,
                (uint) quality);
        }

        /// <summary>
        /// Sets the dictionary for compression and decompression
        /// </summary>
        /// <param name="dictionary">The dictionary as a byte array</param>
        public void SetCustomDictionary(byte[] dictionary) {
            if (dictionary == null)
                throw new ArgumentNullException(nameof(dictionary));

            if (_customDictionary != IntPtr.Zero)
                Marshal.FreeHGlobal(_customDictionary);

            _customDictionary = Marshal.AllocHGlobal(dictionary.Length);
            Marshal.Copy(dictionary, 0, _customDictionary, dictionary.Length);

            if (_mode == CompressionMode.Compress) {
                Brotli.BrotliEncoderSetCustomDictionary(ref _encoderState, dictionary.Length,
                    (byte*) _customDictionary);
            }
            else {
                Brotli.BrotliDecoderSetCustomDictionary(ref _decoderState, dictionary.Length,
                    (byte*) _customDictionary);
            }
        }

        /// <summary>
        /// Sets the window size for the encoder
        /// </summary>
        /// <param name="windowSize">A value from 0-24</param>
        public void SetWindow(int windowSize) {
            if (_mode != CompressionMode.Compress)
                throw new InvalidOperationException("SetWindow is only valid for compress");

            if (windowSize < Brotli.BROTLI_MIN_WINDOW_BITS || windowSize > Brotli.BROTLI_MAX_WINDOW_BITS)
                throw new ArgumentOutOfRangeException(nameof(windowSize), "Window size should be a value between " +
                                                                          Brotli.BROTLI_MIN_WINDOW_BITS + "-" + Brotli
                                                                              .BROTLI_MAX_WINDOW_BITS);

            Brotli.BrotliEncoderSetParameter(ref _encoderState, Brotli.BrotliEncoderParameter.BROTLI_PARAM_LGWIN,
                (uint) windowSize);
        }

        protected override void Dispose(bool disposing) {
            if (disposing && !_disposed) {
                FlushCompress(true);

                if (_mode == CompressionMode.Compress)
                    Brotli.BrotliEncoderDestroyInstance(ref _encoderState);
                else
                    Brotli.BrotliDecoderStateCleanup(ref _decoderState);
                if (_customDictionary != IntPtr.Zero)
                    Marshal.FreeHGlobal(_customDictionary);
                _disposed = true;
            }

            if (disposing && !_leaveOpen && _stream != null) {
                _stream.Close();
                _stream = null;
            }

            base.Dispose(disposing);
        }

        public override void Flush() {
            EnsureNotDisposed();
            FlushCompress(false);
        }

        private void FlushCompress(bool finish) {
            if (_mode != CompressionMode.Compress)
                return;

            if (Brotli.BrotliEncoderIsFinished(ref _encoderState))
                return;

            var op = finish
                ? Brotli.BrotliEncoderOperation.BROTLI_OPERATION_FINISH
                : Brotli.BrotliEncoderOperation.BROTLI_OPERATION_FLUSH;

            byte[] buffer = new byte[0];
            WriteCore(buffer, 0, 0, op);
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public override void SetLength(long value) {
            throw new NotSupportedException();
        }

        private void ValidateParameters(byte[] array, int offset, int count) {
            if (array == null)
                throw new ArgumentNullException("array");

            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset");

            if (count < 0)
                throw new ArgumentOutOfRangeException("count");

            if (array.Length - offset < count)
                throw new ArgumentException("Invalid argument offset and count");
        }

        public override int Read(byte[] buffer, int offset, int count) {
            if (_mode != CompressionMode.Decompress)
                throw new InvalidOperationException("Read is only supported in Decompress mode");

            EnsureNotDisposed();
            ValidateParameters(buffer, offset, count);

            bool endOfStream = false;
            byte[] in_buf = new byte[0xffff];
            size_t available_in = 0, available_out = count;
            fixed (byte* in_buf_ptr = in_buf)
            fixed (byte* buf_ptr = buffer) {
                byte* next_in = in_buf_ptr;
                byte* next_out = buf_ptr + offset;
                int total = 0;

                while (true) {
                    if (_lastDecoderState == Brotli.BrotliDecoderResult.BROTLI_DECODER_RESULT_NEEDS_MORE_INPUT) {
                        int len = _stream.Read(in_buf, 0, in_buf.Length);
                        if (len <= 0) {
                            endOfStream = true;
                            break;
                        }
                        available_in = len;
                        next_in = in_buf_ptr;
                    }
                    else if (_lastDecoderState == Brotli.BrotliDecoderResult.BROTLI_DECODER_RESULT_NEEDS_MORE_OUTPUT) {
                        break; /* Should not occur */
                    }
                    else {
                        endOfStream = true;
                        break;
                    }

                    size_t available_out_old = available_out;
                    _lastDecoderState = Brotli.BrotliDecoderDecompressStream(ref _decoderState, &available_in,
                        &next_in, &available_out, &next_out, null);

                    total += (int) (available_out_old - available_out);
                    if (total >= count) break;
                }

                if (endOfStream && _lastDecoderState != Brotli.BrotliDecoderResult.BROTLI_DECODER_RESULT_SUCCESS)
                    throw new InvalidDataException("Decompression failed with error code: " + _decoderState.error_code);

                return total;
            }
        }

        private void WriteCore(byte[] buffer, int offset, int count, Brotli.BrotliEncoderOperation operation) {
            bool flush = operation == Brotli.BrotliEncoderOperation.BROTLI_OPERATION_FLUSH ||
                         operation == Brotli.BrotliEncoderOperation.BROTLI_OPERATION_FINISH;

            byte[] out_buf = new byte[0x1FFFE];
            size_t available_in = count, available_out = out_buf.Length;
            fixed (byte* out_buf_ptr = out_buf)
            fixed (byte* buf_ptr = buffer)
            {
                byte* next_in = buf_ptr + offset;
                byte* next_out = out_buf_ptr;

                while ((!flush && available_in > 0) || flush)
                {
                    if (!Brotli.BrotliEncoderCompressStream(ref _encoderState,
                        operation, &available_in, &next_in,
                        &available_out, &next_out, null))
                    {
                        throw new InvalidDataException("Compression failed");
                    }

                    bool hasData = available_out != out_buf.Length;
                    if (hasData)
                    {
                        int out_size = (int)(out_buf.Length - available_out);
                        _stream.Write(out_buf, 0, out_size);
                        available_out = out_buf.Length;
                        next_out = out_buf_ptr;
                    }

                    if (Brotli.BrotliEncoderIsFinished(ref _encoderState))
                        break;

                    if (!hasData && flush)
                        break;
                }
            }
        }

        public override void Write(byte[] buffer, int offset, int count) {
            if (_mode != CompressionMode.Compress)
                throw new InvalidOperationException("Write is only supported in Compress mode");

            EnsureNotDisposed();
            ValidateParameters(buffer, offset, count);
            WriteCore(buffer, offset, count, Brotli.BrotliEncoderOperation.BROTLI_OPERATION_PROCESS);
        }

        public override bool CanRead {
            get {
                if (_stream == null)
                    return false;

                return _mode == CompressionMode.Decompress && _stream.CanRead;
            }
        }

        public override bool CanSeek => false;

        public override bool CanWrite {
            get {
                if (_stream == null)
                    return false;

                return _mode == CompressionMode.Compress && _stream.CanWrite;
            }
        }

        public override long Length {
            get {
                throw new NotSupportedException();
            }
        }

        public override long Position {
            get {
                throw new NotSupportedException();
            }
            set {
                throw new NotSupportedException();
            }
        }

        private void EnsureNotDisposed() {
            if (_stream == null)
                throw new ObjectDisposedException(null, "The underlying stream has been disposed");
        }
    }
}
