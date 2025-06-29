using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader
{
    public static class LibOoz
    {
        private static ILogger logger = Log.ForContext(typeof(LibOoz));

        // https://github.com/zao/ooz

        [DllImport("libooz.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Ooz_Decompress(
            byte[] src_buf,
            int src_len,
            byte[] dst,
            ulong dst_size,
            int u1,
            int u2,
            int u3,
            IntPtr u4,
            UIntPtr u5,
            IntPtr u6,
            IntPtr u7,
            IntPtr u8,
            UIntPtr u9,
            int u10
        );

        public static byte[] Ooz_Decompress(byte[] srcBuf, int decompressedSize)
        {
            var resBuf = new byte[decompressedSize + 128]; // (+128 to account for `SAFE_SPACE` constant)
            var res = Ooz_Decompress(srcBuf, srcBuf.Length, resBuf, (ulong)decompressedSize, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            if (res < 0)
                logger.Warning("LibOoz reported a decompression error!");
            return resBuf;
        }
    }
}
