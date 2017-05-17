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
        /// <summary>
        /// By default the first TopN types are printed.
        /// </summary>
        static int TopN = 20;

        /// <summary>
        /// Path to public OneDrive which contains practically from all .NET versions all DAC dlls. 
        /// </summary>
        const string DacCollection = "https://1drv.ms/f/s!AhcFq7XO98yJgoMwuPd7LNioVKAp_A";

        static string HelpStr = String.Format("MemAnalyzer {0} by Alois Kraus 2017", Assembly.GetExecutingAssembly().GetName().Version) + Environment.NewLine +
                                "Usage: MemAnalyzer [ -f DumpFile or -pid ddd [ -f2 DumpFile or -pid2 ddd ] -dts [N] or -dtn [N] or -dstrings [N] [-live] [-unit DisplayUnit] [-vmmap] ] [-gc xxx [-process xxx.exe] ] [-o Output.csv [-sep \t]] [-dump pidOrExe [outputDumpFileOrDir]] " + Environment.NewLine +
                                "       -f fileName          Dump file to analyze." + Environment.NewLine +
                                "       -f2 fileName         Diff Dump files" + Environment.NewLine +
                                "       -pid ddd             Live process to analyze." + Environment.NewLine +
                                "       -pid2 ddd            Live process to diff. You can combined it to e.g. compare a live process with a dump. Subtraction is done -xxx2 - xxxx where xxx is Pid or f" + Environment.NewLine +
                                "       -vmmap               Fetch from live processes VMMAP data. VMMap.exe must be in the path to work." + Environment.NewLine +
                               $"       -dts N               Dump top N types by object size. Default for N is {TopN}." + Environment.NewLine +
                               $"       -dtn N               Dump top N types by object count. Default for N is {TopN}." + Environment.NewLine +
                               $"       -dstrings N          Dump top N duplicate strings and global statistics. Default for N is {TopN}." + Environment.NewLine +
                                "       -unit DisplayUnit    DisplayUnit can be Bytes, KB, MB or GB" + Environment.NewLine +
                                "       -live                If present only reachable (live) objects are considered in the statistics. Takes longer to calculate." + Environment.NewLine +
                                "       -dacdir dir          If the dump file is from a machine with a different version you can tell MemAnalyzer in which directory to search for matching dac dlls." + Environment.NewLine +
                               $"                            See {DacCollection} for a collection of dac dlls from .NET 2.0 up to 4.7." + Environment.NewLine +
                                "       -procdump args       Create a memory dump and VMMap snapshot of a process. Needs procdump.exe and vmmap.exe in the path to work." + Environment.NewLine + 
                                "       -o output.csv        Write output to csv file instead of console" + Environment.NewLine +
                                " Return Value: If -dts/dtn is used it will return the allocated managed memory in KB." + Environment.NewLine +
                                "               If additionally -vmmap is present it will return allocated Managed Heap + Heap + Private + Shareable + File Mappings." + Environment.NewLine +
                                "               That enables leak detection during automated tests which can then e.g. enable allocation profiling on demand." + Environment.NewLine  +
                                "Examples" + Environment.NewLine +
                                "Dump types by size from dump file." + Environment.NewLine +
                                "\tMemAnalyzer -f xx.dmp -dts" + Environment.NewLine +
                                "Dump types by object count from a running process with process id ddd." + Environment.NewLine +
                                "\tMemAnalyzer -pid ddd -dtn" + Environment.NewLine +
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

        string[] ProcDumpArgs = null;

        static bool ShowHelpMessage = true;

        /// <summary>
        /// Contains PID/Dump File name which are currently looking into
        /// </summary>
        TargetInformation TargetInformation = new TargetInformation();

        public bool LiveOnly { get; private set; }

        /// <summary>
        /// If set the output will written to a CSV file.
        /// </summary>
        public string OutFile { get; private set; }

        /// <summary>
        /// When true fetch from live processes VMMap information if VMMap is in the path.
        /// </summary>
        bool GetVMMapData { get; set; }

        DataTarget Target = null;
        DataTarget Target2 = null;

        public static bool IsDebug = false;
        bool IsChild = false;

        string DacDir = null;
        string ProcessNameFilter = null;
        Actions Action = Actions.None;

        DisplayUnit DisplayUnit = DisplayUnit.Bytes;

        static int ReturnCode = 0;

        public Program(string[] args)
        {
            this.Args = args;
        }


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

            DebugPrinter.Write($"Returning value {ReturnCode} as exit code.");
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
                            TargetInformation.DumpFileName1 = NotAnArg(args.Dequeue());
                            break;
                        case "-f2":
                            TargetInformation.DumpFileName2 = NotAnArg(args.Dequeue());
                            break;
                        case "-pid":
                            SetDefaultActionIfNotSet();
                            TargetInformation.Pid1 = int.Parse(NotAnArg(args.Dequeue()));
                            break;
                        case "-child":
                            IsChild = true;
                            break;
                        case "-pid2":
                            TargetInformation.Pid2 = int.Parse(NotAnArg(args.Dequeue()));
                            break;
                        case "-unit":
                            string next = args.Dequeue();
                            if ( Enum.TryParse(NotAnArg(next), true, out DisplayUnit tmpUnit) )
                            {
                                DisplayUnit = tmpUnit;
                            }
                            else
                            {
                                Console.WriteLine($"Warning: DisplayUnit {next} is not a valid value. Using default: {DisplayUnit}");
                            }
                            break;
                        case "-vmmap":
                            GetVMMapData = true;
                            break;
                        case "-procdump":
                            Action = Actions.ProcDump;
                            // give remaining args to procdump and to not try to parse them by ourself
                            ProcDumpArgs = args.ToArray();
                            args.Clear();
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
                            DacDir = NotAnArg(args.Dequeue());
                            // quoted mscordacwks file paths are not correctly treated so far. 
                            Environment.SetEnvironmentVariable("_NT_SYMBOL_PATH", DacDir.Trim(new char[] { '"' }));
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
            AddProcessStartDirectoryToPath();

            MemAnalyzerBase analyzer = null;
            ShowHelpMessage = false; // When we now throw exceptions it is not due to wrong command line arguments. 
            try
            {
                if (!String.IsNullOrEmpty(OutFile))
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
                        int? allocatedKB = (analyzer as MemAnalyzer)?.DumpTypes(TopN, false);
                        if (allocatedKB != null)
                        {
                            ReturnCode = allocatedKB.Value;
                        }
                        break;
                    case Actions.DumpTypesBySize:
                        allocatedKB = (analyzer as MemAnalyzer)?.DumpTypes(TopN, true);
                        if (allocatedKB != null)
                        {
                            ReturnCode = allocatedKB.Value;
                        }
                        break;
                    case Actions.DumpStrings:
                        (analyzer as StringStatisticsCommand)?.Execute(TopN);
                        break;
                    case Actions.ProcDump:
                        DumpCreator dumper = new DumpCreator(IsDebug);
                        dumper.Dump(ProcDumpArgs);
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Command {0} is not recognized as a valid command", this.Action));
                }
            }
            finally
            {
                OutputStringWriter.Flush();

                if (OutputStringWriter.CsvOutput)
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
                if (Target2 != null)
                {
                    Target2.Dispose();
                }
            }
        }

        /// <summary>
        /// Fixes issues with tools deployed side by side with the executable but the current working directory might 
        /// be different.
        /// </summary>
        private static void AddProcessStartDirectoryToPath()
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            path += ";" + Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            Environment.SetEnvironmentVariable("PATH", path);
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
            catch (Exception ex)
            {
                DebugPrinter.Write("Got from CreateRuntime or GetHeap: {0}", ex);

                // If it is a target architecture mismatch hide error message and start x64 to try again later.
                if( Environment.Is64BitProcess )
                {
                    Console.WriteLine("Error: Is the dump file opened by another process (debugger)? If yes close the debugger first.");
                    Console.WriteLine("       If the dump comes from a different computer with another CLR version {0} that you are running on your machine you need to download the matching mscordacwks.dll first. Check out " + DacCollection + " and download the matching version/s.", clrVersion.Version);
                    Console.WriteLine("       Then set _NT_SYMBOL_PATH=PathToYourDownloadedMscordackwks.dll  e.g. _NT_SYMBOL_PATH=c:\\temp\\mscordacwks in the shell where you did execute MemAnalyzer and then try again.");
                    Console.WriteLine();
                }
                throw;
            }
        }

        private void RestartWithx64()
        {
            if( Environment.Is64BitProcess)
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
            DebugPrinter.Write($"Setting return code from x64 process {p.ExitCode} to our own return code.");
            ReturnCode = p.ExitCode;
        }

        MemAnalyzerBase CreateAnalyzer(Actions action)
        {
            ClrHeap heap = null;
            ClrHeap heap2 = null;

            try
            {
                if (TargetInformation.DumpFileName1 != null)
                {
                    // If only one dump file is opened then we can open the file with the debugger interface which only supports
                    // one reader in one process. This is useful because otherwise we will lock the file and ClrMD will error 
                    // out if the dump file already open within a debugger which is pretty common.
                    Target = DataTarget.LoadCrashDump(TargetInformation.DumpFileName1, TargetInformation.DumpFileName2 == null ? CrashDumpReader.DbgEng : CrashDumpReader.ClrMD);
                    heap = GetHeap(Target);
                }
                if (TargetInformation.DumpFileName2 != null)
                {
                    Target2 = DataTarget.LoadCrashDump(TargetInformation.DumpFileName2, CrashDumpReader.ClrMD);
                    heap2 = GetHeap(Target2);
                }

                if (TargetInformation.Pid1 != 0 && Target == null)
                {
                    if( !NativeMethods.ProcessExists(TargetInformation.Pid1) )
                    {
                        Console.WriteLine($"Error: Process {TargetInformation.Pid1} is not running.");
                        return null;
                    }

                    // do not try wrong bitness when we know it will fail anyway
                    if ( NativeMethods.IsWin64(TargetInformation.Pid1) && !Environment.Is64BitProcess) 
                    {
                        DebugPrinter.Write($"Starting x64 because Pid {TargetInformation.Pid1} is Win64 process");
                        RestartWithx64();
                        return null;
                    }

                    Target = DataTarget.AttachToProcess(TargetInformation.Pid1, 5000u);
                    heap = GetHeap(Target);
                    if (heap == null)
                    {
                        Console.WriteLine($"Error: Could not get managed heap of process {TargetInformation.Pid1}. Most probably it is an unmanaged process.");
                    }
                }

                if (TargetInformation.Pid2 != 0 & Target2 == null)
                {
                    if (!NativeMethods.ProcessExists(TargetInformation.Pid2))
                    {
                        Console.WriteLine($"Error: Process {TargetInformation.Pid2} is not running.");
                        return null;
                    }

                    // Cannot load data from processes with different bitness
                    if(TargetInformation.Pid1 != 0 && (NativeMethods.IsWin64(TargetInformation.Pid1) != NativeMethods.IsWin64(TargetInformation.Pid2) ) )
                    {
                        Console.WriteLine($"Error: Process {TargetInformation.Pid1} and {TargetInformation.Pid2} are of different bitness. You can dump each one separately to CSV files and compare the CSV files instead.");
                        return null;
                    }

                    // do not try wrong bitness when we know it will fail anyway
                    if ( NativeMethods.IsWin64(TargetInformation.Pid2) && !Environment.Is64BitProcess)
                    {
                        DebugPrinter.Write($"Starting x64 because Pid2 {TargetInformation.Pid2} is Win64 process");
                        RestartWithx64();
                        return null;
                    }

                    Target2 = DataTarget.AttachToProcess(TargetInformation.Pid2, 5000u);
                    heap2 = GetHeap(Target2);
                }
            }
            catch(Exception ex)
            {
                // Default is a 32 bit process if runtime creation fails with InvalidOperationException or a DAC location failure
                // we try x64 as fall back. This will work if the bitness of the dump file is wrong.
                if(!Environment.Is64BitProcess && (ex is FileNotFoundException || ex is InvalidOperationException || ex is ClrDiagnosticsException ||
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
                        return new MemAnalyzer(heap, heap2, LiveOnly, GetVMMapData, TargetInformation, DisplayUnit);
                    case Actions.DumpStrings:
                        return new StringStatisticsCommand(heap, heap2, LiveOnly, DisplayUnit);
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
            DumpStrings,
            ProcDump,
        }
    }
}
