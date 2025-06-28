using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.SaveReader
{
    public class LibOoz
    {
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

        public static int Ooz_Decompress(byte[] srcBuf, byte[] dstBuf) => Ooz_Decompress(srcBuf, srcBuf.Length, dstBuf, (ulong)dstBuf.Length, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }
}
