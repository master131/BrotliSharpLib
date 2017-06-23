using System;
using size_t = BrotliSharpLib.Brotli.SizeT;
using BrotliEncoderState = BrotliSharpLib.Brotli.BrotliEncoderStateStruct;

namespace BrotliSharpLib {
    public static partial class Brotli {
        private static unsafe BrotliEncoderState BrotliEncoderCreateInstance(brotli_alloc_func alloc_func,
            brotli_free_func free_func, void* opaque) {
            BrotliEncoderState state = CreateStruct<BrotliEncoderState>();
            BrotliInitMemoryManager(
                ref state.memory_manager_, alloc_func, free_func, opaque);
            BrotliEncoderInitState(ref state);
            return state;
        }

        private static void BrotliEncoderInitParams(ref BrotliEncoderParams params_) {
            params_.mode = BROTLI_DEFAULT_MODE;
            params_.quality = BROTLI_DEFAULT_QUALITY;
            params_.lgwin = BROTLI_DEFAULT_WINDOW;
            params_.lgblock = 0;
            params_.size_hint = 0;
            params_.disable_literal_context_modeling = false;
        }

        private static unsafe void BrotliEncoderInitState(ref BrotliEncoderState s) {
            BrotliEncoderInitParams(ref s.params_);
            s.input_pos_ = 0;
            s.num_commands_ = 0;
            s.num_literals_ = 0;
            s.last_insert_len_ = 0;
            s.last_flush_pos_ = 0;
            s.last_processed_pos_ = 0;
            s.prev_byte_ = 0;
            s.prev_byte2_ = 0;
            s.storage_size_ = 0;
            s.storage_ = null;
            s.hasher_ = null;
            s.large_table_ = null;
            s.large_table_size_ = 0;
            s.cmd_code_numbits_ = 0;
            s.command_buf_ = null;
            s.literal_buf_ = null;
            s.next_out_ = null;
            s.available_out_ = 0;
            s.total_out_ = 0;
            s.stream_state_ = BrotliEncoderStreamState.BROTLI_STREAM_PROCESSING;
            s.is_last_block_emitted_ = false;
            s.is_initialized_ = false;

            RingBufferInit(ref s.ringbuffer_);

            s.commands_ = null;
            s.cmd_alloc_size_ = 0;

            /* Initialize distance cache. */
            fixed (int* dc = s.dist_cache_)
            fixed (int* sdc = s.saved_dist_cache_) {
                dc[0] = 4;
                dc[1] = 11;
                dc[2] = 15;
                dc[3] = 16;
                /* Save the state of the distance cache in case we need to restore it for
                   emitting an uncompressed block. */
                memcpy(sdc, dc, sizeof(int) * 4);
            }
        }

        private static bool BrotliEncoderSetParameter(
            ref BrotliEncoderState state, BrotliEncoderParameter p, uint value)
        {
            /* Changing parameters on the fly is not implemented yet. */
            if (state.is_initialized_) return false;
            /* TODO: Validate/clamp parameters here. */
            switch (p)
            {
                case BrotliEncoderParameter.BROTLI_PARAM_MODE:
                    state.params_.mode = (BrotliEncoderMode)value;
                    return false;

                case BrotliEncoderParameter.BROTLI_PARAM_QUALITY:
                    state.params_.quality = (int)value;
                    return true;

                case BrotliEncoderParameter.BROTLI_PARAM_LGWIN:
                    state.params_.lgwin = (int)value;
                    return true;

                case BrotliEncoderParameter.BROTLI_PARAM_LGBLOCK:
                    state.params_.lgblock = (int)value;
                    return true;

                case BrotliEncoderParameter.BROTLI_PARAM_DISABLE_LITERAL_CONTEXT_MODELING:
                    if ((value != 0) && (value != 1)) return false;
                    state.params_.disable_literal_context_modeling = value != 0;
                    return true;

                case BrotliEncoderParameter.BROTLI_PARAM_SIZE_HINT:
                    state.params_.size_hint = value;
                    return true;

                default: return false;
            }
        }

        private static void EncodeWindowBits(int lgwin, out byte last_byte,
            out byte last_byte_bits)
        {
            if (lgwin == 16)
            {
                last_byte = 0;
                last_byte_bits = 1;
            }
            else if (lgwin == 17)
            {
                last_byte = 1;
                last_byte_bits = 7;
            }
            else if (lgwin > 17)
            {
                last_byte = (byte)(((lgwin - 17) << 1) | 1);
                last_byte_bits = 4;
            }
            else
            {
                last_byte = (byte)(((lgwin - 8) << 4) | 1);
                last_byte_bits = 7;
            }
        }

        /* Initializes the command and distance prefix codes for the first block. */
        private static unsafe void InitCommandPrefixCodes(byte* cmd_depths,
            ushort* cmd_bits,
            byte* cmd_code,
            size_t* cmd_code_numbits) {

            fixed (byte* kdcd = kDefaultCommandDepths)
                memcpy(cmd_depths, kdcd, kDefaultCommandDepths.Length);

            fixed (ushort* kdcb = kDefaultCommandBits)
                memcpy(cmd_bits, kdcb, kDefaultCommandBits.Length * sizeof(ushort));

            /* Initialize the pre-compressed form of the command and distance prefix
               codes. */
            fixed (byte* kdcc = kDefaultCommandCode)
                memcpy(cmd_code, kdcc, kDefaultCommandCode.Length);
            *cmd_code_numbits = kDefaultCommandCodeNumBits;
        }

        private static unsafe bool EnsureInitialized(ref BrotliEncoderState s)
        {
            if (s.is_initialized_) return true;

            fixed (BrotliEncoderParams* params_ = &s.params_) {
                SanitizeParams(params_);
                s.params_.lgblock = ComputeLgBlock(params_);

                s.remaining_metadata_bytes_ = uint.MaxValue;

                fixed (RingBuffer* rb = &s.ringbuffer_)
                    RingBufferSetup(params_, rb);

                /* Initialize last byte with stream header. */
                {
                    int lgwin = s.params_.lgwin;
                    if (params_->quality == FAST_ONE_PASS_COMPRESSION_QUALITY ||
                        params_->quality == FAST_TWO_PASS_COMPRESSION_QUALITY) {
                        lgwin = Math.Max(lgwin, 18);
                    }
                    EncodeWindowBits(lgwin, out s.last_byte_, out s.last_byte_bits_);
                }

                if (params_->quality == FAST_ONE_PASS_COMPRESSION_QUALITY) {
                    fixed (byte* cmd_depths = s.cmd_depths_)
                    fixed (ushort* cmd_bits = s.cmd_bits_)
                    fixed (byte* cmd_code = s.cmd_code_)
                    fixed (size_t* cmd_code_numbits = &s.cmd_code_numbits_)
                        InitCommandPrefixCodes(cmd_depths, cmd_bits,
                            cmd_code, cmd_code_numbits);
                }
            }

            s.is_initialized_ = true;
            return true;
        }

        /*
           Copies the given input data to the internal ring buffer of the compressor.
           No processing of the data occurs at this time and this function can be
           called multiple times before calling WriteBrotliData() to process the
           accumulated input. At most input_block_size() bytes of input data can be
           copied to the ring buffer, otherwise the next WriteBrotliData() will fail.
         */
        private static unsafe void CopyInputToRingBuffer(ref BrotliEncoderState s,
            size_t input_size,
            byte* input_buffer) {
            if (!EnsureInitialized(ref s)) return;
            fixed (RingBuffer* ringbuffer_ = &s.ringbuffer_) {
                RingBufferWrite(ref s.memory_manager_, input_buffer, input_size, ringbuffer_);
                s.input_pos_ += input_size;

                /* TL;DR: If needed, initialize 7 more bytes in the ring buffer to make the
                   hashing not depend on uninitialized data. This makes compression
                   deterministic and it prevents uninitialized memory warnings in Valgrind.
                   Even without erasing, the output would be valid (but nondeterministic).
              
                   Background information: The compressor stores short (at most 8 bytes)
                   substrings of the input already read in a hash table, and detects
                   repetitions by looking up such substrings in the hash table. If it
                   can find a substring, it checks whether the substring is really there
                   in the ring buffer (or it's just a hash collision). Should the hash
                   table become corrupt, this check makes sure that the output is
                   still valid, albeit the compression ratio would be bad.
              
                   The compressor populates the hash table from the ring buffer as it's
                   reading new bytes from the input. However, at the last few indexes of
                   the ring buffer, there are not enough bytes to build full-length
                   substrings from. Since the hash table always contains full-length
                   substrings, we erase with dummy zeros here to make sure that those
                   substrings will contain zeros at the end instead of uninitialized
                   data.
              
                   Please note that erasing is not necessary (because the
                   memory region is already initialized since he ring buffer
                   has a `tail' that holds a copy of the beginning,) so we
                   skip erasing if we have already gone around at least once in
                   the ring buffer.
              
                   Only clear during the first round of ring-buffer writes. On
                   subsequent rounds data in the ring-buffer would be affected. */
                if (ringbuffer_->pos_ <= ringbuffer_->mask_) {
                    /* This is the first time when the ring buffer is being written.
                       We clear 7 bytes just after the bytes that have been copied from
                       the input buffer.
                
                       The ring-buffer has a "tail" that holds a copy of the beginning,
                       but only once the ring buffer has been fully written once, i.e.,
                       pos <= mask. For the first time, we need to write values
                       in this tail (where index may be larger than mask), so that
                       we have exactly defined behavior and don't read uninitialized
                       memory. Due to performance reasons, hashing reads data using a
                       LOAD64, which can go 7 bytes beyond the bytes written in the
                       ring-buffer. */
                    memset(ringbuffer_->buffer_ + ringbuffer_->pos_, 0, 7);
                }
            }
        }

        private static unsafe void BrotliEncoderSetCustomDictionary(ref BrotliEncoderState s, size_t size, byte* dict) {
            size_t max_dict_size = BROTLI_MAX_BACKWARD_LIMIT(s.params_.lgwin);
            size_t dict_size = size;

            if (!EnsureInitialized(ref s)) return;

            if (dict_size == 0 ||
                s.params_.quality == FAST_ONE_PASS_COMPRESSION_QUALITY ||
                s.params_.quality == FAST_TWO_PASS_COMPRESSION_QUALITY) {
                return;
            }
            if (size > max_dict_size) {
                dict += size - max_dict_size;
                dict_size = max_dict_size;
            }
            CopyInputToRingBuffer(ref s, dict_size, dict);
            s.last_flush_pos_ = dict_size;
            s.last_processed_pos_ = dict_size;
            if (dict_size > 0) {
                s.prev_byte_ = dict[dict_size - 1];
            }
            if (dict_size > 1) {
                s.prev_byte2_ = dict[dict_size - 2];
            }
            fixed (HasherHandle* hasher_ = &s.hasher_)
            fixed (BrotliEncoderParams* params_ = &s.params_)
                HasherPrependCustomDictionary(ref s.memory_manager_, hasher_, params_, dict_size, dict);
        }
    }
}