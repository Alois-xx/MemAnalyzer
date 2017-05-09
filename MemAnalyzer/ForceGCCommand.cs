using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MemAnalyzer
{
    /// <summary>
    /// This class assumes that you have already used PerfView 1.9.0 on your machine. It simply calls into
    /// HeapDump from PerfView.
    /// </summary>
    class ForceGCCommand
    {
        /// <summary>
        /// The version is part of the file name where HeapDump is located. Can be overridden
        /// </summary>
        static string PerfViewVersion = Environment.GetEnvironmentVariable("PERFVIEW_VERSION") ?? "VER.2016-02-21.20.41.11.631";

        string HeapDumpx64Path = Environment.ExpandEnvironmentVariables(String.Format(@"%APPDATA%\PerfView\{0}\AMD64\HeapDump.exe", PerfViewVersion));
        string HeapDumpx86Path = Environment.ExpandEnvironmentVariables(String.Format(@"%APPDATA%\PerfView\{0}\x86\HeapDump.exe", PerfViewVersion));

        static int CurrentPid = Process.GetCurrentProcess().Id;

        ProcessFilter Filter;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="processNameFilter">Can be null or a substring of an executable name.</param>
        /// <param name="cmdLineFilter">Substring of a command line parameter</param>
        /// <param name="pid">Process id</param>
        public ForceGCCommand(string processNameFilter, string cmdLineFilter, int pid)
        {
            if( pid == 0 && String.IsNullOrEmpty(cmdLineFilter) && String.IsNullOrEmpty(processNameFilter) )
            {
               throw new ArgumentException("Process filter was invalid (pid == 0 -gc and -process has no args!");
            }

            if( !(String.IsNullOrEmpty(cmdLineFilter) && String.IsNullOrEmpty(processNameFilter)) )
            {
                Filter = new ProcessFilter(processNameFilter, cmdLineFilter);
            }
            else
            {
                Filter = new ProcessFilter(pid);
            }
        }

        void CheckPreConditions()
        {
            if( !IsPerfViewInstalled() )
            {
                throw new FileNotFoundException("PerfView must be in the path. Cannot continue.");
            }

        }

        private bool IsPerfViewInstalled()
        {
            ProcessStartInfo info = new ProcessStartInfo("PerfView.exe", "mark \"Test\"  /Logfile:\"%temp%\\perfviewDummy.log\"")
            {
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = true
            };

            try
            {
                var p = Process.Start(info);
                p.WaitForExit();

            }
            catch(FileNotFoundException)
            {
                return false;
            }
            
            return true;
        }


        public void ForceGC()
        {
            bool bFound = false;
            foreach(var pid in Filter.GetMatchingPids().Where(pid => pid != CurrentPid))
            {
                bFound = true;
                ForceGC(pid);
            }
            if(!bFound )
            {
                Console.WriteLine("Warning: No process did match the cmd line filter: {0}, process name filter: {1} or pid filter: {2}", 
                    (this.Filter.CmdLineFilters != null && this.Filter.CmdLineFilters.Length>0) ? this.Filter.CmdLineFilters[0] : "none",  
                    this.Filter.ProcessNameFilter ?? "none", 
                    this.Filter.PidFilter == 0 ? "none" : this.Filter.PidFilter.ToString());
            }
        }

        private void ForceGC(int pid)
        {
            using(var p = Process.GetProcessById(pid))
            {
                Console.WriteLine("Force GC in process {0}, {1}", pid, p.ProcessName);

                ProcessStartInfo start = null;
                string heapDumpArg = String.Format("/forceGC {0}", pid);
                start = new ProcessStartInfo(NativeMethods.IsWin64(p) ? HeapDumpx64Path : HeapDumpx86Path, heapDumpArg)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                if( !File.Exists(start.FileName) )
                {
                    throw new FileNotFoundException(
                        String.Format("Could not find PerfView Heapdumper at: {0}." + Environment.NewLine +
                                      "Could be version mismatch of a newer Perfview version. Check out the target directory and set the environment variable PERFVIEW_VERSION to your current version e.g. set PERFVIEW_VERSION=VER.2015-10-19.10.47.15.934" + Environment.NewLine +
                                      "Or download the latest PerfView version from https://www.microsoft.com/en-us/download/details.aspx?id=28567 and start it once to deploy HeapDump.exe from PerfView."));
                }

                using(var heapDump = Process.Start(start))
                {
                    string output = heapDump.StandardOutput.ReadToEnd();
                    if (!output.Contains("Done forcing GCs success=True"))
                        Console.WriteLine(output);
                    heapDump.WaitForExit();
                }
            }
        }


    }
}
