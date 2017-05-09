using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MemAnalyzer
{
    static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWow64Process([In] IntPtr process, [Out] out bool wow64Process);

        public static bool IsWin64(Process process)
        {
            bool retVal = false;
            bool success = NativeMethods.IsWow64Process(process.Handle, out retVal);
            if (success)
            {
                return !retVal;
            }
            else
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not determine process bitness.");
            }
        }

        public static bool IsWin64(int pid)
        {
            return IsWin64(Process.GetProcessById(pid));
        }


        public static bool ProcessExists(int pid)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

    }
}
