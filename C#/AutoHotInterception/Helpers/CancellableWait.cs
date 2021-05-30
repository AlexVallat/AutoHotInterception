using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace AutoHotInterception.Helpers
{
	public static class CancellableWait
	{
        private static uint INFINITE = 0xFFFFFFFF;
        private const uint WAIT_TIMEOUT = 0x102;
        private const uint WAIT_FAILED = 0xFFFFFFFF;

        [DllImport("kernel32.dll")]
        private static extern uint WaitForMultipleObjects(uint nCount, IntPtr[] lpHandles, bool bWaitAll, uint dwMilliseconds);


        [StructLayout(LayoutKind.Sequential)]
        private struct InterceptionDevice
		{
            public IntPtr DeviceHandle;
            public IntPtr WaitHandle;
		}

        private const int INTERCEPTION_MAX_DEVICE = 20;
        public static int Wait(IntPtr context, CancellationToken cancellation)
		{
            var size = Marshal.SizeOf(new InterceptionDevice());

            var waitHandles = new List<IntPtr>(INTERCEPTION_MAX_DEVICE + 1);
            var waitedDeviceIndices = new List<int>(INTERCEPTION_MAX_DEVICE);
            for (var i = 0; i < INTERCEPTION_MAX_DEVICE; i++)
			{
                var device = Marshal.PtrToStructure<InterceptionDevice>(context + (size * i));
                if (device.WaitHandle != IntPtr.Zero)
                {
                    waitHandles.Add(device.WaitHandle);
                    waitedDeviceIndices.Add(i);
                }
			}

            if (cancellation.IsCancellationRequested)
			{
                return 0;
			}
            waitHandles.Add(cancellation.WaitHandle.SafeWaitHandle.DangerousGetHandle());
            var result = WaitForMultipleObjects((uint)waitHandles.Count, waitHandles.ToArray(), false, INFINITE);

            if (result == WAIT_FAILED || result == WAIT_TIMEOUT ||
                result == waitHandles.Count - 1) // The cancellation handle
            {
                return 0;
            }

            return waitedDeviceIndices[(int)result] + 1;
        }
	}
}
