using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MemAnalyzer
{
    class Program
    {
        static int TopN = 20;
        const string DacCollection = "https://1drv.ms/f/s!AhcFq7XO98yJgoMwuPd7LNioVKAp_A";

        static string HelpStr = String.Format("MemAnalyzer {0} by Alois Kraus 2017", Assembly.GetExecutingAssembly().GetName().Version) + Environment.NewLine +
                                "Usage: MemAnalyzer [ -f DumpFile or -pid ddd [ -f2 DumpFile or -pid2 ddd ] -dts [N] or -dtn [N] or -dstrings [N] [-live] ] [-gc xxx [-process xxx.exe]] [-o Output.csv [-sep \t]]" + Environment.NewLine +
                                "       -f fileName          Dump file to analyze." + Environment.NewLine +
                                "       -f2 fileName         Diff Dump files" + Environment.NewLine +
                                "       -pid ddd             Live process to analyze." + Environment.NewLine +
                                "       -pid2 ddd            Live process to diff. You can combined it to e.g. compare a live process with a dump. Subtraction is done -xxx2 - xxxx where xxx is Pid or f" + Environment.NewLine +
                               $"       -dts N               Dump top N types by object size. Default for N is {TopN}." + Environment.NewLine +
                               $"       -dtn N               Dump top N types by object count. Default for N is {TopN}." + Environment.NewLine +
                               $"       -dstrings N          Dump top N duplicate strings and global statistics. Default for N is {TopN}." + Environment.NewLine +
                                "       -live                If present only reachable (live) objects are considered in the statistics. Takes longer to calculate." + Environment.NewLine +
                                "       -dacdir dir          If the dump file is from a machine with a different version you can tell MemAnalyzer in which directory to search for matching dac dlls." + Environment.NewLine +
                               $"                            See {DacCollection} for a collection of dac dlls from .NET 2.0 up to 4.7." + Environment.NewLine +
                                "       -gc xxx              Force GC in process with id or if xxx is not a number it is treated as a command line substring filter." + Environment.NewLine +
                                "       -process xxx.exe     (optional) Name of executable in which a GC should happen. Must contain .exe in its name." + Environment.NewLine +
                                "       -o output.csv        Write output to csv file instead of console" + Environment.NewLine +
                                "Examples" + Environment.NewLine +
                                "Dump types by size from dump file." + Environment.NewLine +
                                "\tMemAnalyzer -f xx.dmp -dts" + Environment.NewLine +
                                "Dump types by object count from a running process with process id ddd." + Environment.NewLine +
                                "\tMemAnalyzer -pid ddd -dts" + Environment.NewLine +
                                "Diff two memory dump files where (f2 - f) are calculated." + Environment.NewLine +
                                "\tMemAnalyzer -f dump1.dmp -f2 dump2.dmp -dts" + Environment.NewLine +
                                "Dump string duplicates of live process and write it to CSV file" + Environment.NewLine +
                                "\tMemAnalyzer -pid ddd -dstrings -o StringDuplicates.csv" + Environment.NewLine;

        /// <summary>
        /// Returned by COMException if dump could not be loaded into current address space. 
        /// This happens usually when a 64 bit dump is loaded into a 32 bit process or if two x86 dumps are loaded for comparison.
        /// </summary>
        const int HResultNotEnoughStorage = -2147024888;

        private string[] Args;

        static bool ShowHelpMessage = true;

        string DumpFile = null;
        string DumpFile2 { get; set; }
        int Pid2 { get; set; }
        public bool LiveOnly { get; private set; }
        public string OutFile { get; private set; }

        DataTarget Target = null;
        DataTarget Target2 = null;

        public static bool IsDebug = false;
        bool IsChild = false;
        int Pid = 0;
        string DacDll = null;
        string CmdLineFilter = null;
        string ProcessNameFilter = null;
        Actions Action = Actions.None;

        public Program(string[] args)
        {
            this.Args = args;
        }

        static int ReturnCode = 0;

        static int Main(string[] args)
        {
            try
            {
                AppDomainResolverFromResources resolver = new AppDomainResolverFromResources(typeof(Binaries));
                var p = new Program(args);
                if (p.Parse())
                {
                    p.Run();
                }
            }
            catch (Exception ex)
            {
                ReturnCode = -1;
                Help("Got Exception: {0}", ex);
            }

            return ReturnCode;
        }

        static void Help(string msg = null, params object[] args)
        {
            if (ShowHelpMessage)
            {
                Console.WriteLine(HelpStr);
            }
            if (msg != null)
            {
                Console.WriteLine(msg, args);
            }
        }




        private bool Parse()
        {
            Queue<string> args = new Queue<string>(Args);
            if( args.Count == 0)
            {
                Help("You need to specify a dump file or live process to analyze.");
                return false;
            }

            bool lret = true;
            try
            {
                while (args.Count > 0)
                {
                    string param = args.Dequeue();
                    switch (param.ToLower())
                    {
                        case "-f":
                            SetDefaultActionIfNotSet();
                            DumpFile = NotAnArg(args.Dequeue());
                            break;
                        case "-f2":
                            DumpFile2 = NotAnArg(args.Dequeue());
                            break;
                        case "-pid":
                            SetDefaultActionIfNotSet();
                            Pid = int.Parse(NotAnArg(args.Dequeue()));
                            break;
                        case "-child":
                            IsChild = true;
                            break;
                        case "-pid2":
                            Pid2 = int.Parse(NotAnArg(args.Dequeue()));
                            break;
                        case "-debug":
                            IsDebug = true;
                            break;
                        case "-sep":
                            string sep = NotAnArg(args.Dequeue());
                            sep = sep.Trim(new char[] { '"', '\'' });
                            sep = sep == "\\t" ? "\t" : sep;
                            if( sep.Length != 1)
                            {
                                Console.WriteLine($"Warning CSV separator character \"{sep}\" was not recognized. Using default \t");
                            }
                            else
                            {
                                OutputStringWriter.SeparatorChar = sep[0];
                            }
                            break;
                        case "-gc":
                            Action = Actions.ForceGC;
                            CmdLineFilter = args.Dequeue();
                            CmdLineFilter = int.TryParse(CmdLineFilter, out Pid) ? null : CmdLineFilter;
                            break;
                        case "-dts":
                            Action = Actions.DumpTypesBySize;
                            if ( args.Count > 0 && NotAnArg(args.Peek(),false) != null)
                            {
                                TopN = int.Parse(args.Dequeue());
                            }
                            break;
                        case "-dtn":
                            Action = Actions.DumpTypesByCount;
                            if (args.Count > 0 && NotAnArg(args.Peek(),false) != null)
                            {
                                TopN = int.Parse(args.Dequeue());
                            }
                            break;
                        case "-live":
                            LiveOnly = true;
                            break;
                        case "-dacdir":
                            DacDll = NotAnArg(args.Dequeue());
                            // quoted mscordacwks file paths are not correctly treated so far. 
                            Environment.SetEnvironmentVariable("_NT_SYMBOL_PATH", DacDll.Trim(new char[] { '"' }));
                            break;
                        case "-process":
                            ProcessNameFilter = NotAnArg(args.Dequeue());
                            break;
                        case "-dstrings":
                            Action = Actions.DumpStrings;
                            if (args.Count > 0 && NotAnArg(args.Peek(), false) != null)
                            {
                                TopN = int.Parse(args.Dequeue());
                            }
                            break;
                        case "-o":
                            OutFile = NotAnArg(args.Dequeue());
                            TopN = 20 * 1000; // if output is dumped to file pipe all types to output
                                              // if later -dts/-dstrings/-dtn is specified one can still override this behavior.
                            break;
                        default:
                            throw new ArgNotExpectedException(param);
                    }
                    
                }
            }
            catch(ArgNotExpectedException ex)
            {
                Help("Unexpected command line argument: {0}", ex.Message);
                lret = false;
            }

            return lret;
        }

        string NotAnArg(string arg, bool bthrow=true)
        {
            if( arg.StartsWith("-") )
            {
                if (bthrow)
                {
                    throw new ArgNotExpectedException(arg);
                }
                else
                {
                    return null;
                }
            }

            return arg;
        }

        private void Run()
        {
            MemAnalyzerBase analyzer = null;
            ShowHelpMessage = false; // When we now throw exceptions it is not due to wrong command line arguments. 
            try
            {
                if( !String.IsNullOrEmpty(OutFile))
                {
                    OutputStringWriter.CsvOutput = true;
                    OutputStringWriter.Output = new StreamWriter(OutFile);
                }
                analyzer = CreateAnalyzer(Action);
                switch (Action)
                {
                    case Actions.None:
                        Help("No command specified.");
                        break;
                    case Actions.DumpTypesByCount:
                        (analyzer as MemAnalyzer)?.DumpTypes(TopN, false);
                        break;
                    case Actions.DumpTypesBySize:
                        (analyzer as MemAnalyzer)?.DumpTypes(TopN, true);
                        break;
                    case Actions.ForceGC:
                        ForceGCCommand cmd = new ForceGCCommand(ProcessNameFilter, CmdLineFilter, Pid);
                        cmd.ForceGC();
                        break;
                    case Actions.DumpStrings:
                        (analyzer as StringStatisticsCommand)?.Execute(TopN);
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Command {0} is not recognized as a valid command", this.Action));
                }
            }
            finally
            {
                OutputStringWriter.Flush();

                if( OutputStringWriter.CsvOutput )
                {
                    Console.WriteLine($"Writing output to csv file {OutFile}");
                }

                if (analyzer != null)
                {
                    analyzer.Dispose();
                }
                if (Target != null)
                {
                    Target.Dispose();
                }
                if( Target2 != null)
                {
                    Target2.Dispose();
                }
            }
        }

        public ClrHeap GetHeap(DataTarget target)
        {
            var clrVersion = target.ClrVersions.FirstOrDefault();
            if (clrVersion == null)
            {
                return null;
            }

            ClrRuntime runtime = null;
            try
            {
                runtime = clrVersion.CreateRuntime();
                return runtime.GetHeap();
            }
            catch (Exception)
            {
                Console.WriteLine("Error: Is the dump file opened by another process (debugger)? If yes close the debugger first.");
                Console.WriteLine("       If the dump comes from a different computer with another CLR version {0} that you are running on your machine you need to download the matching mscordacwks.dll first. Check out " + DacCollection + " and download the matching version/s.", clrVersion.Version);
                Console.WriteLine("       Then set _NT_SYMBOL_PATH=PathToYourDownloadedMscordackwks.dll  e.g. _NT_SYMBOL_PATH=c:\\temp\\mscordacwks in the shell where you did execute MemAnalyzer and then try again.");
                Console.WriteLine();
                throw;
            }
        }

        private void RestartWithx64()
        {
            if( IntPtr.Size == 8)
            {
                return;
            }

            if( IsChild )
            {
                Console.WriteLine("Recursion detected. Do not start any further child process. Please file an issue at https://github.com/Alois-xx/MemAnalyzer/issues");
                return;
            }

            // If csv output file was already opened close it to allow child process to write to it.
            if (OutputStringWriter.CsvOutput == true)
            {
                OutputStringWriter.Output.Close();
                OutputStringWriter.Output = Console.Out;
                OutputStringWriter.CsvOutput = false;
            }

            string exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MemAnalyzerx64.exe");

            // if -debug is specified do not deploy the exe into AppData. Instead start it from the location where where the main exe is located
            if( !IsDebug )
            {
                string appDir = Environment.ExpandEnvironmentVariables($"%AppData%\\MemAnalyzer\\{Assembly.GetExecutingAssembly().GetName().Version}");
                Directory.CreateDirectory(appDir);
                exe = Path.Combine(appDir, "MemAnalyzerx64.exe");
            }

            if (!File.Exists(exe))
            {
                File.WriteAllBytes(exe, Binaries.MemAnalyzerx64);
                // Deploy app.config
                File.WriteAllText(exe + ".config", Binaries.App);
            }

            DebugPrinter.Write("Starting x64 child {0}", exe);
            ProcessStartInfo info = new ProcessStartInfo(exe, GetQuotedArgs() + " -child")
            {
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var p = Process.Start(info);
            string output = null;
            while( (output =  p.StandardOutput.ReadLine()) != null )
            {
                Console.WriteLine(output);
            }
            p.WaitForExit();
        }

        MemAnalyzerBase CreateAnalyzer(Actions action)
        {
            ClrHeap heap = null;
            ClrHeap heap2 = null;

            try
            {
                if (this.DumpFile != null)
                {
                    Target = DataTarget.LoadCrashDump(DumpFile, CrashDumpReader.ClrMD);
                    heap = GetHeap(Target);
                }
                if (this.DumpFile2 != null)
                {
                    Target2 = DataTarget.LoadCrashDump(DumpFile2, CrashDumpReader.ClrMD);
                    heap2 = GetHeap(Target2);
                }

                if (Pid != 0 && Target == null)
                {
                    if( !NativeMethods.ProcessExists(Pid) )
                    {
                        Console.WriteLine($"Error: Process {Pid} is not running.");
                        return null;
                    }

                    // do not try wrong bitness when we know it will fail anyway
                    if ( NativeMethods.IsWin64(Pid) && IntPtr.Size == 4) 
                    {
                        DebugPrinter.Write($"Starting x64 because Pid {Pid} is Win64 process");
                        RestartWithx64();
                        return null;
                    }

                    Target = DataTarget.AttachToProcess(Pid, 5000u);
                    heap = GetHeap(Target);
                    if (heap == null)
                    {
                        Console.WriteLine($"Error: Could not get managed heap of process {Pid}. Most probably it is an unmanaged process.");
                    }
                }

                if (Pid2 != 0 & Target2 == null)
                {
                    if (!NativeMethods.ProcessExists(Pid2))
                    {
                        Console.WriteLine($"Error: Process {Pid2} is not running.");
                        return null;
                    }

                    // Cannot load data from processes with different bitness
                    if(Pid != 0 && (NativeMethods.IsWin64(Pid) != NativeMethods.IsWin64(Pid2) ) )
                    {
                        Console.WriteLine($"Error: Process {Pid} and {Pid2} are of different bitness. You can dump each one separately to CSV files and compare the CSV files instead.");
                        return null;
                    }

                    // do not try wrong bitness when we know it will fail anyway
                    if ( NativeMethods.IsWin64(Pid2) && IntPtr.Size == 4 )
                    {
                        DebugPrinter.Write($"Starting x64 because Pid2 {Pid2} is Win64 process");
                        RestartWithx64();
                        return null;
                    }

                    Target2 = DataTarget.AttachToProcess(Pid2, 5000u);
                    heap2 = GetHeap(Target2);
                }
            }
            catch(Exception ex)
            {
                // Default is a 32 bit process if runtime creation fails with InvalidOperationException or a DAC location failure
                // we try x64 as fall back. This will work if the bitness of the dump file is wrong.
                if(IntPtr.Size == 4 && (ex is FileNotFoundException || ex is InvalidOperationException || ex is ClrDiagnosticsException ||
                                       (ex is COMException && ex.HResult == HResultNotEnoughStorage)) ) 
                {
                    RestartWithx64();
                }
               else
                {
                    throw;
                }
            }

            if (heap != null)
            {
                switch (action)
                {
                    case Actions.DumpTypesByCount:
                    case Actions.DumpTypesBySize:
                        return new MemAnalyzer(heap, heap2, LiveOnly);
                    case Actions.DumpStrings:
                        return new StringStatisticsCommand(heap, heap2, LiveOnly);
                    case Actions.ForceGC:
                        return null;
                    case Actions.None:
                        return null;
                    default:
                        return null;
                }
            }
            else
            {
                return null;
            }
        }

        static string GetQuotedArgs()
        {
            string args = "";
            string currArg;
            foreach (var arg in Environment.GetCommandLineArgs().Skip(1))
            {
                currArg = arg;
                if (currArg.Contains(" "))
                {
                    currArg = "\"" + currArg + "\"";
                }

                args += currArg + " ";
            }

            return args;
        }

        void SetDefaultActionIfNotSet()
        {
            if (Action == Actions.None)
            {
                Action = Actions.DumpTypesBySize;
            }
        }
        enum Actions
        {
            None = 0,
            DumpTypesByCount,
            DumpTypesBySize,
            ForceGC,
            DumpStrings,
        }
    }
}
