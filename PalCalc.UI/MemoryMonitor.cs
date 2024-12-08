using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PalCalc.UI
{
    internal class MemoryMonitor : IDisposable
    {
        CancellationTokenSource disposalTokenSource;
        Thread monitorThread;

        public bool PauseNotices;

        public event Action MemoryWarning;

        /* Main Interface */
        public MemoryMonitor(CancellationToken token)
        {
            PauseNotices = false;
            disposalTokenSource = new CancellationTokenSource();

            monitorThread = new Thread(() =>
            {
                using var combinedSource = CancellationTokenSource.CreateLinkedTokenSource(token, disposalTokenSource.Token);
                var waitHandle = combinedSource.Token.WaitHandle;

                while (!combinedSource.IsCancellationRequested)
                {
                    if (waitHandle.WaitOne(TimeSpan.FromMilliseconds(100))) break;

                    var freeMem = FreeSystemMemory;
                    var totalMem = TotalSystemMemory;
                    var selfMem = ProcessMemory;

                    var shouldalert = (
                        // less than 20% RAM is available and we're using over 25% of system RAM
                        (freeMem < totalMem * 0.2 && selfMem > totalMem * 0.25) ||

                        // less than 5% of RAM available
                        freeMem < totalMem * 0.05 ||

                        // less than 1GB of RAM available
                        // (fallback just in case)
                        freeMem < (1000 * 1000 * 1000)
                    );

                    if (!PauseNotices && shouldalert)
                        MemoryWarning?.Invoke();
                }
            })
            {
                Priority = ThreadPriority.AboveNormal
            };

            monitorThread.Start();
        }

        public void Stop()
        {
            if (monitorThread == null) return;

            disposalTokenSource.Cancel();
            monitorThread.Join();
            monitorThread = null;

            disposalTokenSource.Dispose();
        }

        public void Dispose()
        {
            Stop();
        }

        /* Utils */

        // https://stackoverflow.com/a/105109
        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX()
            {
                this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        private static MEMORYSTATUSEX MemoryStatus
        {
            get
            {
                var res = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(res)) return res;
                else return null;
            }
        }

        public static ulong TotalSystemMemory => MemoryStatus.ullTotalPhys;
        public static ulong FreeSystemMemory => MemoryStatus.ullAvailPhys;
        public static ulong ProcessMemory => (ulong)Environment.WorkingSet;
    }
}
