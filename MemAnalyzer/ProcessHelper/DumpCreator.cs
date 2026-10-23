using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
        // procdump.exe is the documented build. procdumpw.exe is the native build which is needed on ARM64 and
        // procdump64.exe is the x64 build. All of them print the same progress lines, so any one of them works.
        static readonly string[] ProcDumpCandidates = new string[] { "procdump.exe", "procdumpw.exe", "procdump64.exe" };

        static readonly string[] ErrorPhrases = new string[]
        {
            "Error creating dump file", "Error:", "No process matching", "Access is denied",
            "Unable to open", "Cannot create", "Failed to", "not a valid", "Unknown option",
        };

        bool ShowOutput = false;
        bool VerifyDump = false;

        /// <summary>
        /// VMMap data of the snapshot which was written next to the created dump file. Empty if the pid of the
        /// dumped process could not be determined or if vmmap.exe is not in the path.
        /// </summary>
        internal VMMapData VmMapData { get; private set; } = new VMMapData();

        public DumpCreator(bool showOutput, bool verifyDump)
        {
            ShowOutput = showOutput;
            VerifyDump = verifyDump;
        }

        /// <summary>
        /// Create a memory dump with procdump and then call VMMap to dump the memory constituents into a csv file besides the dump.
        /// </summary>
        /// <param name="procdumpArgs"></param>
        /// <returns>Full path of the created dump file or null if procdump did not create one.</returns>
        public string Dump(string[] procdumpArgs)
        {
            string exe = FindProcDump();

            if (exe == null)
            {
                Console.WriteLine($"Error: {ProcDumpCandidates[0]} was not found in the path or in the folder of MemAnalyzer.");
                Console.WriteLine($"       Install Sysinternals procdump ({string.Join(" or ", ProcDumpCandidates)}) and try again.");
                return null;
            }

            string args = GetProcDumpArgs(procdumpArgs);

            ProcessStartInfo info = new ProcessStartInfo(exe, $"-accepteula {args}")
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true, 
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            Process p;
            try
            {
                p = Process.Start(info);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: Could not start {exe} with args: {args}");
                Console.WriteLine($"       {ex.Message}");
                return null;
            }

            string line;
            string dumpFileName = null;
            List<string> lines = new List<string>();
            bool procDumpError = false;
            bool procdumpUsage = false;

            while (true)
            {
                // procdump writes UTF-16 when its output is redirected. Strip the null characters so the text
                // can be parsed independent of the encoding procdump used.
                line = CleanLine(p.StandardOutput.ReadLine());

                if (line == null)
                {
                    break;
                }

                lines.Add(line);
                if (ShowOutput)
                {
                    Console.WriteLine(line);
                }

                if (IsProcDumpErrorLine(line))
                {
                    procDumpError = true;
                }

                // The usage text of procdump contains the default dump file name placeholder. That is no file name.
                if (IsProcDumpUsageLine(line))
                {
                    procdumpUsage = true;
                }

                if (dumpFileName == null && procDumpError == false)
                { 
                    dumpFileName = GetDumpFileName(line);
                }
            }

            string error = CleanLine(p.StandardError.ReadToEnd());
            if (!String.IsNullOrEmpty(error))
            {
                lines.Add(error);
                if (ShowOutput)
                {
                    Console.WriteLine(error);
                }
                if (IsProcDumpErrorLine(error))
                {
                    procDumpError = true;
                }
            }

            p.WaitForExit();
            int exitCode = p.ExitCode;

            if (dumpFileName == null)
            {
                if(!ShowOutput)
                {
                    lines.ForEach(Console.WriteLine);
                }
                Console.WriteLine($"Error: Could not create dump file with procdump args: {args}!");
                if (procdumpUsage)
                {
                    Console.WriteLine("Error: procdump printed its usage text because an arg after -procdump is not a procdump switch. Everything after -procdump is handed to procdump.exe, MemAnalyzer options belong before -procdump.");
                }
                if (exitCode != 0)
                {
                    Console.WriteLine($"Error: {Path.GetFileName(exe)} exited with exit code {exitCode}.");
                }
                return null;
            }

            // procdump can print a relative file name. Make it absolute so the file is found in a later run.
            dumpFileName = Path.GetFullPath(dumpFileName);

            Console.WriteLine($"Dump file {dumpFileName} created.");

            int pid = ResolveTargetPid(procdumpArgs, out string exeName);

            if (pid != 0)
            {
                string outFile = TargetInformation.GetAssociatedVMMapFile(dumpFileName);
                VMMap.SaveVMmapDataToFile(pid, outFile);
                // Parse the snapshot which was just written so the VMMap rows can be reported like for a live process run.
                VmMapData = new VMMap(outFile).GetMappingData();
            }
            else
            {
                Console.WriteLine($"Error: Could not create find process id of dumped process {exeName}. No VMMap information is saved. ");
            }

            if (VerifyDump && !CanLoadDump(dumpFileName))
            {
                Console.WriteLine($"Error: Dump file cannot be parsed with MemAnalyzer. Managed Heap might be in an inconsistent state.");
                dumpFileName = null;
            }


            return dumpFileName;
        }

        /// <summary>
        /// Find procdump in the path or in the folder of the running MemAnalyzer executable.
        /// </summary>
        /// <returns>Full path of the procdump executable or null if none of the known builds was found.</returns>
        internal static string FindProcDump()
        {
            // GetEntryAssembly() is null when MemAnalyzer is used from a test host, so use the base directory.
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";

            foreach (string candidate in ProcDumpCandidates)
            {
                string local = Path.Combine(dir, candidate);
                if (File.Exists(local))
                {
                    return local;
                }

                foreach (string entry in path.Split(';'))
                {
                    if (String.IsNullOrEmpty(entry))
                    {
                        continue;
                    }

                    string full = Path.Combine(entry.Trim(), candidate);
                    if (File.Exists(full))
                    {
                        return full;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Remove the null characters which procdump writes when its output is redirected as UTF-16.
        /// </summary>
        /// <param name="line">Line as it was read from procdump. Can be null.</param>
        /// <returns>Text of the line without null characters or null if the line is null.</returns>
        internal static string CleanLine(string line)
        {
            if (line == null)
            {
                return null;
            }

            if (line.IndexOf('\0') < 0)
            {
                return line;
            }

            return line.Replace("\0", "");
        }

        /// <summary>
        /// Check if a procdump output line reports a problem.
        /// </summary>
        /// <param name="line">Cleaned procdump output line.</param>
        /// <returns>True if procdump reported an error with this line.</returns>
        internal static bool IsProcDumpErrorLine(string line)
        {
            return ErrorPhrases.Any(p => line.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Check if a procdump output line belongs to the usage text which procdump prints when an argument is invalid.
        /// </summary>
        /// <param name="line">Cleaned procdump output line.</param>
        /// <returns>True if this line belongs to the usage text of procdump.</returns>
        internal static bool IsProcDumpUsageLine(string line)
        {
            return line.Contains("Default dump filename:") || line.Contains("Usage: procdump.exe") ||
                   line.Contains("Usage: procdumpw.exe");
        }

        private bool CanLoadDump(string dumpFileName)
        {
            ProcessStartInfo info = new ProcessStartInfo(Assembly.GetEntryAssembly().Location, $"-f {dumpFileName} -vmmap")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            var p = Process.Start(info);
            Console.WriteLine("Verifying Dump if it is readable ...");
            Console.WriteLine(p.StandardOutput.ReadToEnd());
            DebugPrinter.Write($"Start child process {info.FileName} with args: {info.Arguments}");
            p.WaitForExit();
            return p.ExitCode > 0;
        }

        /// <summary>
        /// Get the pid of the process which procdump will dump. If no pid is part of the procdump args the
        /// executable name is used to find a matching running process.
        /// </summary>
        /// <param name="procdumpArgs"></param>
        /// <param name="exeName">Executable name which was dumped.</param>
        /// <returns>Pid of the process which was dumped. If it is 0 only exeName is known.</returns>
        internal static int ResolveTargetPid(string[] procdumpArgs, out string exeName)
        {
            int pid = FindPidInProcDumpArgs(procdumpArgs, out exeName);

            if (pid == 0)
            {
                // procdump accepts the executable name without the .exe extension. ProcessFilter compares the WMI
                // process name exactly, so add the extension here when it is missing.
                string name = exeName ?? "";
                if (name.Length > 0 && !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    name += ".exe";
                }

                ProcessFilter filter = new ProcessFilter(name);
                pid = filter.GetMatchingPids().FirstOrDefault();
            }

            return pid;
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


        /// <summary>
        /// Get the dump file name from a procdump progress line like
        /// "[12:31:25] Dump 1 initiated: C:\temp\notepad.dmp". The usage text of procdump contains the placeholder
        /// "Default dump filename: PROCESSNAME_YYMMDD_HHMMSS.dmp" which is not a file name, so lines which are not
        /// procdump progress lines return null.
        /// </summary>
        /// <param name="line">Line which procdump wrote to stdout.</param>
        /// <returns>Dump file name or null if the line is not a procdump progress line.</returns>
        internal string GetDumpFileName(string line)
        {
            line = CleanLine(line);

            // "initiated:" is a marker without spaces, the older "Writing dump file" contains them.
            string marker = "initiated:";
            int pos = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

            if (pos < 0)
            {
                // Older procdump versions printed "Writing dump file C:\temp\notepad.dmp ..."
                marker = "Writing dump file";
                pos = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            }

            if (pos < 0)
            {
                return null;
            }

            int start = pos + marker.Length;
            while (start < line.Length && line[start] == ' ')
            {
                start++;
            }

            if (start >= line.Length)
            {
                return null;
            }

            int end = line.IndexOf(' ', start);
            string file = end < 0 ? line.Substring(start) : line.Substring(start, end - start);

            return file.Trim(new char[] { '"', '\'' });
        }
    }
}
