using MemAnalyzer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace MemAnalyzer_uTest
{
    public class DumpCreatorTests
    {
        const string StringAllocExe = "StringAllocatorx86.exe";
        string dumpFileName = "test.dmp";

        // xunit runs the constructor before every test (replaces [TestInitialize])
        public DumpCreatorTests()
        {
            if( File.Exists(dumpFileName) )
            {
                File.Delete(dumpFileName);
            }

            string vmmapFile = TargetInformation.GetExistingVMMapFile(dumpFileName);
            if( vmmapFile != null)
            {
                File.Delete(vmmapFile);
            }

            Program.IsDebug = true;
        }

        [Fact]
        public void Can_Dump_Process()
        {
            DumpCreator creator = new DumpCreator(true,false);
            var process = AllocStrings();

            string file = creator.Dump(new string[] { process.Id.ToString(), "test.dmp" });
            Assert.NotNull(file);
            Assert.True(File.Exists(file), "Dump file does not exist!");
            string associatedVmmap = TargetInformation.GetExistingVMMapFile(file);
            Assert.True(associatedVmmap != null, "Associated VMMap dump file was not found. Is VMMap.exe in path?");
            // The -vmmap rows are reported from this parsed snapshot when -procdump and -vmmap are combined.
            Assert.True(creator.VmMapData.HasValues, "VMMap data of the dumped process was not parsed.");
        }

        [Fact]
        public void Can_Parse_RegularPid()
        {
            string[] args = new string[] { "-ma", "5000" };
            int pid = DumpCreator.FindPidInProcDumpArgs(args, out string exeName);
            Assert.Equal(5000, pid);
        }

        [Fact]
        public void IfExeAndDumpFile_Present_Ignore_Dump()
        {
            string[] args = new string[] { "-ma", "notepad", "notepad.dmp" };
            int pid = DumpCreator.FindPidInProcDumpArgs(args, out string exeName);
            Assert.Equal("notepad", exeName);
        }

        [Fact]
        public void ExeFilePresent_DumpFileNot()
        {
            string[] args = new string[] { "-ma", "notepad" };
            int pid = DumpCreator.FindPidInProcDumpArgs(args, out string exeName);
            Assert.Equal("notepad", exeName);
        }


        [Fact]
        public void Can_Parse_RegularexeName()
        {
            string[] args = new string[] { "-ma", "notepad.exe" };
            int pid = DumpCreator.FindPidInProcDumpArgs(args, out string exeName);
            Assert.Equal(0, pid);
            Assert.Equal("notepad.exe", exeName);
        }

        [Fact]
        public void Can_Parse_RegularexeNameSecondLast()
        {
            string[] args = new string[] { "notepad.exe", "-ma" };
            int pid = DumpCreator.FindPidInProcDumpArgs(args, out string exeName);
            Assert.Equal(0, pid);
            Assert.Equal("notepad.exe", exeName);
        }



        [Fact]
        public void Can_Parse_SinglePidArg()
        {
            string[] args = new string[] { "5000" };
            int pid = DumpCreator.FindPidInProcDumpArgs(args, out string exeName);
            Assert.Equal(5000, pid);
        }

        [Fact]
        public void Procdump_Usage_Line_Is_Not_A_Dump_File_Name()
        {
            DumpCreator creator = new DumpCreator(false, false);

            // This line is part of the procdump usage text and was previously treated as the created dump file.
            Assert.Null(creator.GetDumpFileName("   Default dump filename: PROCESSNAME_YYMMDD_HHMMSS.dmp"));

            // The progress line which procdump prints when it writes a dump.
            Assert.Equal(@"C:\temp\notepad.dmp", creator.GetDumpFileName("[12:31:25] Dump 1 initiated: C:\\temp\\notepad.dmp"));
        }

        [Fact]
        public void MemAnalyzer_Options_After_ProcDump_Are_Not_Passed_To_Procdump()
        {
            Assert.True(Program.IsMemAnalyzerSwitchOnly("-vmmap"));
            Assert.True(Program.IsMemAnalyzerSwitchOnly("-dts"));

            // procdump understands these switches, they stay with procdump (-f filter, -o overwrite dump file).
            Assert.False(Program.IsMemAnalyzerSwitchOnly("-ma"));
            Assert.False(Program.IsMemAnalyzerSwitchOnly("-f"));
            Assert.False(Program.IsMemAnalyzerSwitchOnly("-o"));
            Assert.False(Program.IsMemAnalyzerSwitchOnly("notepad.exe"));
        }

        [Fact]
        public void ProcDump_Args_Keep_Only_Procdump_Args()
        {
            Program p = new Program(new string[] { "-procdump", "-vmmap", "-ma", "2600", @"C:\temp\notepad.dmp" });

            Assert.True(p.Parse());
            Assert.Equal(new string[] { "-ma", "2600", @"C:\temp\notepad.dmp" }, p.ProcDumpArgs.ToArray());
        }


        [Fact]
        public void Procdump_Output_With_Null_Chars_Is_Readable()
        {
            // procdump writes UTF-16 when its output is redirected. The null chars must not break the parsing.
            string nulLine = "[12:31:25] D\0u\0m\0p\0 1 initiated: C:\\temp\\notepad.dmp";
            Assert.Equal("[12:31:25] Dump 1 initiated: C:\\temp\\notepad.dmp", DumpCreator.CleanLine(nulLine));

            DumpCreator creator = new DumpCreator(false, false);
            Assert.Equal(@"C:\temp\notepad.dmp", creator.GetDumpFileName(nulLine));
            Assert.Null(DumpCreator.CleanLine(null));
        }

        [Fact]
        public void Procdump_Progress_Format_Without_Space_Is_Parsed()
        {
            DumpCreator creator = new DumpCreator(false, false);

            // procdump v12 prints the timestamp without a space after the closing bracket.
            Assert.Equal(@"C:\temp\notepad.dmp", creator.GetDumpFileName("[13:05:50]Dump 1 initiated: C:\\temp\\notepad.dmp"));

            // Older procdump versions printed the progress differently.
            Assert.Equal(@"C:\temp\notepad.dmp", creator.GetDumpFileName("Writing dump file C:\\temp\\notepad.dmp of 100 MB"));
        }

        [Fact]
        public void Procdump_Problems_And_Usage_Are_Detected()
        {
            Assert.True(DumpCreator.IsProcDumpErrorLine("Error: No process matching the specified PID can be found."));
            Assert.True(DumpCreator.IsProcDumpErrorLine("Error creating dump file for process 1234"));
            Assert.False(DumpCreator.IsProcDumpErrorLine("[13:05:50]Dump 1 complete: 72 MB written in 0.1 seconds"));

            Assert.True(DumpCreator.IsProcDumpUsageLine("   Default dump filename: PROCESSNAME_YYMMDD_HHMMSS.dmp"));
            Assert.True(DumpCreator.IsProcDumpUsageLine("Usage: procdump.exe [-mm] [-ma] [-n <Count>]"));
            Assert.False(DumpCreator.IsProcDumpUsageLine("[13:05:50]Dump 1 initiated: C:\\temp\\notepad.dmp"));
        }


        internal static Process AllocStrings(string exe= StringAllocExe, int n=0)
        {
            ProcessStartInfo info = new ProcessStartInfo(exe, $"{n}")
            {
                CreateNoWindow= true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            var p = Process.Start(info);
            string line = null;
            while( (line = p.StandardOutput.ReadLine()) != null)
            {
                if( line.Contains("All strings allocated"))
                {
                    break;
                }
            }

            return p;
        }
    }
}
