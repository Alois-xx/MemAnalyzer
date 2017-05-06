using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MemAnalyzer
{
    class Program
    {
        static int TopN = 20;

        static string HelpStr = String.Format("MemAnalyzer {0} by Alois Kraus 2017", Assembly.GetExecutingAssembly().GetName().Version) + Environment.NewLine +
                                "Usage: MemAnalyzer [ -f DumpFile or -pid ddd [ -f2 DumpFile or -pid2 ddd ] -dts [N] or -dtn [N] or -dstring [N] [-live] ] [-gc xxx [-process xxx.exe]] [-o Output.csv [-sep \t]]" + Environment.NewLine +
                                "       -f fileName          Dump file to analyze." + Environment.NewLine +
                                "       -f2 fileName         Diff Dump files" + Environment.NewLine +
                                "       -pid ddd             Live process to analyze." + Environment.NewLine +
                                "       -pid2 ddd            Live process to diff. You can combined it to e.g. compare a live process with a dump. Subtraction is done -xxx2 - xxxx where xxx is Pid or f" + Environment.NewLine +
                               $"       -dts N               Dump top N types by object size. Default for N is {TopN}." + Environment.NewLine +
                               $"       -dtn N               Dump top N types by object count. Default for N is {TopN}." + Environment.NewLine +
                               $"       -dstrings N          Dump top N duplicate strings and global statistics. Default for N is {TopN}." + Environment.NewLine +
                                "       -live                If present only reachable (live) objects are considered in the statistics. Takes longer to calculate." + Environment.NewLine +
                                "       -gc xxx or \"\"        Force GC in process with id or if xxx is not a number it is treated as a command line substring filter. E.g. -forceGC GenericReader" + Environment.NewLine +
                                "                            will force a GC in all generic reader processes. Use \"\" as filter if you use -process to force a GC in all executables." + Environment.NewLine +
                                "       -process xxx.exe     (optional) Name of executable in which a GC should happen. Must contain .exe in its name." + Environment.NewLine +
                                "       -o output.csv        Write output to csv file instead of console" + Environment.NewLine + 
                                "Examples" + Environment.NewLine +
                                "Dump types by size from dump file." + Environment.NewLine +
                                "\tMemAnalyzer -f xx.dmp -dts" + Environment.NewLine +
                                "Dump types by object count from a running process with process id ddd." + Environment.NewLine +
                                "\tMemAnalyzer -pid ddd -dts" + Environment.NewLine +
                                "Diff two memory dump files where (f2 - f) are calculated." + Environment.NewLine +
                                "\tMemAnalyzer -f dump1.dmp -f2 dump2.dmp -dts";



        private string[] Args;

        string DumpFile = null;
        string DumpFile2 { get; set; }
        int Pid2 { get; set; }
        public bool LiveOnly { get; private set; }
        public string OutFile { get; private set; }

        DataTarget Target = null;
        DataTarget Target2 = null;

        int Pid = 0;
        string DacDll = null;
        string CmdLineFilter = null;
        string ProcessNameFilter = null;
        Actions Action = Actions.None;

        public Program(string[] args)
        {
            this.Args = args;
        }

        static void Main(string[] args)
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
            catch(Exception ex)
            {
                Help("Got Exception: {0}", ex);
            }
        }

        static void Help(string msg=null, params object[] args)
        {
            Console.WriteLine(HelpStr);
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
                            DumpFile = NotAnArg(args.Dequeue());
                            break;
                        case "-f2":
                            DumpFile2 = NotAnArg(args.Dequeue());
                            break;
                        case "-pid":
                            Pid = int.Parse(NotAnArg(args.Dequeue()));
                            break;
                        case "-pid2":
                            Pid2 = int.Parse(NotAnArg(args.Dequeue()));
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
                        case "-dac":
                            DacDll = NotAnArg(args.Dequeue());
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
            try
            {
                if( !String.IsNullOrEmpty(OutFile))
                {
                    Console.WriteLine($"Writing output to csv file {OutFile}");
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
                        (analyzer as MemAnalyzer).DumpTypes(TopN, false);
                        break;
                    case Actions.DumpTypesBySize:
                        (analyzer as MemAnalyzer).DumpTypes(TopN, true);
                        break;
                    case Actions.ForceGC:
                        ForceGCCommand cmd = new ForceGCCommand(ProcessNameFilter, CmdLineFilter, Pid);
                        cmd.ForceGC();
                        break;
                    case Actions.DumpStrings:
                        (analyzer as StringStatisticsCommand).Execute(TopN);
                        break;
                    default:
                        throw new NotSupportedException(String.Format("Command {0} is not recognized as a valid command", this.Action));
                }
                
            }
            finally
            {
                OutputStringWriter.Flush();

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
            }
            catch (Exception)
            {
                Console.WriteLine("Error: Is the dump file opened by another process (debugger)? If yes close the debugger first.");
                Console.WriteLine("       If the dump comes from a different computer with another CLR version that you are running on your machine you need to download the matching mscordacwks.dll first. Check out https://1drv.ms/f/s!AhcFq7XO98yJgoMwuPd7LNioVKAp_A and download the matching version/s.", clrVersion.Version);
                Console.WriteLine("       Then set _NT_SYMBOL_PATH=PathToYourDownloadedMscordackwks.dll  e.g. _NT_SYMBOL_PATH=c:\\temp\\mscordacwks in the shell where you did execute MemAnalyzer and then try again.");
                Console.WriteLine();
                throw;
            }
            return runtime.GetHeap();
        }

        MemAnalyzerBase CreateAnalyzer(Actions action)
        {
            ClrHeap heap = null;
            ClrHeap heap2 = null;

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

            if( Pid != 0 && Target == null)
            {
                Target = DataTarget.AttachToProcess(Pid, 5000u);
                heap = GetHeap(Target);
            }

            if( Pid2 != 0 & Target2 == null )
            {
                Target2 = DataTarget.AttachToProcess(Pid2, 5000u);
                heap2 = GetHeap(Target2);
            }

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
