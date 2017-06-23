using System.Runtime.InteropServices;

namespace BrotliSharpLib {
    public static partial class Brotli {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Command {
            public uint insert_len_;
            /* Stores copy_len in low 24 bits and copy_len XOR copy_code in high 8 bit. */
            public uint copy_len_;
            public uint dist_extra_;
            public uint cmd_prefix_;
            public uint dist_prefix_;
        }
    }
}