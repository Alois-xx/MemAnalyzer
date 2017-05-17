using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MemAnalyzer
{
    /// <summary>
    /// Wrapper around procdump which also calls VMMap to get everything we need to diff later an existing dump with a VMMap.csv file.
    /// </summary>
    public class DumpCreator
    {
        const string ProcDumpExe = "procdump.exe";
        bool ShowOutput = false;

        public DumpCreator(bool showOutput)
        {
            ShowOutput = showOutput;
        }

        /// <summary>
        /// Create a memory dump with procdump and then call VMMap to dump the memory constituents into a csv file besides the dump.
        /// </summary>
        /// <param name="procdumpArgs"></param>
        /// <returns></returns>
        public string Dump(string[] procdumpArgs)
        {
            string args = GetProcDumpArgs(procdumpArgs);

            ProcessStartInfo info = new ProcessStartInfo(ProcDumpExe, $"-accepteula {args}")
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true, 
                UseShellExecute = false,
            };

            var p = Process.Start(info);
            string line;
            string dumpFileName = null;
            List<string> lines = new List<string>();
            while( (line = p.StandardOutput.ReadLine()) != null )
            {
                lines.Add(line);
                if (ShowOutput)
                {
                    Console.WriteLine(line);
                }

                if (dumpFileName == null)
                { 
                    dumpFileName = GetDumpFileName(line);
                }
            }

            if( dumpFileName == null )
            {
                if(!ShowOutput)
                {
                    lines.ForEach(Console.WriteLine);
                }
                Console.WriteLine($"Error: Could not create dump file with procdump args: {args}!");
                return null;
            }
            else
            {
                Console.WriteLine($"Dump file {dumpFileName} created.");
            }


            int pid = FindPidInProcDumpArgs(procdumpArgs, out string exeName);

            if(pid == 0)
            {
                ProcessFilter filter = new ProcessFilter(exeName ?? "");
                pid = filter.GetMatchingPids().FirstOrDefault();
            }

            if (pid != 0)
            {
                string outFile = TargetInformation.GetAssociatedVMMapFile(dumpFileName);
                VMMap.SaveVMmapDataToFile(pid, outFile);
            }
            else
            {
                Console.WriteLine($"Error: Could not create find process id of dumped process {exeName}. No VMMap information is saved. ");
            }

            return dumpFileName;
        }

        /// <summary>
        /// Todo: make better. Currently we simply parse the last two args to procdump an try to find a pid or an executable name in them.
        /// </summary>
        /// <param name="procdumpArgs"></param>
        /// <param name="exeName">Executable name which was dumped.</param>
        /// <returns>process id of process which was dumped. If 0 then exeName should contains the executable name which was dumped.</returns>
        internal static int FindPidInProcDumpArgs(string[] procdumpArgs, out string exeName)
        {
            exeName = null;
            bool bUseNext = false;

            foreach(string arg in procdumpArgs.Reverse().Take(2))
            {
                if( int.TryParse(arg, out int pid))
                {
                    return pid;
                }
                else if( arg.IndexOf(".exe", StringComparison.OrdinalIgnoreCase) != -1)
                {
                    exeName = arg;
                }
                else if( arg.IndexOf(".dmp", StringComparison.OrdinalIgnoreCase) != -1)
                {
                    bUseNext = true;
                }
                else if( bUseNext )
                {
                    exeName = arg;
                    break;
                }

            }

            if( exeName == null )
            {
                exeName = procdumpArgs.LastOrDefault();
            }

            return 0;
        }

        /// <summary>
        /// Add quotes around procdump arguments which contain spaces.
        /// </summary>
        /// <param name="procdumpArgs"></param>
        /// <returns></returns>
        private string GetProcDumpArgs(string[] procdumpArgs)
        {
            StringBuilder sb = new StringBuilder();
            foreach(var arg in procdumpArgs)
            {
                if( arg.Contains(' '))
                {
                    sb.Append($"\"{arg}\" ");
                }
                else
                {
                    sb.Append(arg);
                    sb.Append(' ');
                }
            }

            return sb.ToString();
        }


        internal string GetDumpFileName(string line)
        {
            string lret = null;
            if (line.Contains(".dmp"))
            {
                lret = line.Substring(line.IndexOf(" ", line.IndexOf("initiated:") + 1) + 1);
            }

            return lret;
        }
    }
}
