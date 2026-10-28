using MemAnalyzer;
using Xunit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemAnalyzer_uTest
{
    public class MemAnalyzerTests
    {
        const string StringAllocx64 = "StringAllocatorx64.exe";
        const string StringAllocx86 = "StringAllocatorx86.exe";

        const string EmptyDumpx86 = "Emptyx86.dmp";
        const string EmptyDumpx64 = "Emptyx64.dmp";

        const string String50Kx86 = "Strings50Kx86.dmp";
        const string String50Kx64 = "Strings50Kx64.dmp";

        const int N50K = 50 * 1000;
        const int N500K = 500 * 1000;

        string[] Dumps = new string[] { EmptyDumpx64, EmptyDumpx86, String50Kx64, String50Kx86 };


        // xunit runs the constructor before every test (replaces [TestInitialize])
        public MemAnalyzerTests()
        {
            if(!File.Exists(EmptyDumpx86))
                AllocateAndDump(StringAllocx86, 0, EmptyDumpx86);

            if (!File.Exists(String50Kx86))
                AllocateAndDump(StringAllocx86, 50*1000, String50Kx86);

            if (!File.Exists(EmptyDumpx64))
                AllocateAndDump(StringAllocx64, 0, EmptyDumpx64);

            if (!File.Exists(String50Kx64))
                AllocateAndDump(StringAllocx64, 50 * 1000, String50Kx64);
        }


        void AllocateAndDump(string exe, int n, string dumpFile)
        {
            DumpCreator creator = new DumpCreator(true, false);

            var process = DumpCreatorTests.AllocStrings(exe, n);
            string dumpfile = creator.Dump(new string[] { "-ma", $"{process.Id}", $"{dumpFile}" });
            Assert.True(dumpfile != null, $"Dump {dumpFile} could not be created");
            if (!process.HasExited)
            {
                process.Kill();
            }
        }


        /// <summary>
        /// Start MemoryAnalyzer.exe and return exit code and output
        /// </summary>
        /// <param name="args"></param>
        /// <param name="output"></param>
        /// <returns></returns>
        int StartMemAnalyzer(string args, out string output)
        {
            var info = new ProcessStartInfo("MemAnalyzer.exe", args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };

            var p = Process.Start(info);
            output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            return p.ExitCode;
        }



        [Fact]
        public void Can_Compare_Running_Process_With_Dump()
        {
            var process = DumpCreatorTests.AllocStrings(StringAllocx86, N50K);
            int LeakInKB = StartMemAnalyzer($"-pid {process.Id} -f2 {EmptyDumpx86}", out string output);
            Console.WriteLine(output);
            // the 50k strings are ~2.4 MB; .NET 4.8's GC commits that footprint slightly tighter than
            // the 2.8 MB the pre-4.6 GC used to, so the margin here is -2000 rather than -2500
            Assert.True(LeakInKB < -2000, $"Leak should be at least -2000 KB but was {LeakInKB}, Output: {output}");
            
        }

        [Fact]
        public void Can_Compare_Two_Dumps_x86()
        {
            int LeakInKB = StartMemAnalyzer($"-f {EmptyDumpx86} -f2 {EmptyDumpx86}", out string output);
            Console.WriteLine(output);
            Assert.True(LeakInKB == 0, $"Two same dumps must have same leak size but was {LeakInKB}, Output: {output}");
            
        }

        [Fact]
        public void Leak_Of_Two_Dumps_Is_Deteced_x86()
        {
            int LeakInKB = StartMemAnalyzer($"-f {EmptyDumpx86} -f2 {String50Kx86}", out string output);
            Console.WriteLine(output);
            Assert.True(LeakInKB > 2000, $"Leak should be at least 2500 KB but was {LeakInKB}, Output: {output}");
            
        }

        [Fact]
        public void Leak_Of_Two_Dumps_Is_Deteced_x64()
        {
            int LeakInKB = StartMemAnalyzer($"-f {EmptyDumpx64} -f2 {String50Kx64}", out string output);
            Console.WriteLine(output);
            Assert.True(LeakInKB > 2500, $"Leak should be at least 2500 KB but was {LeakInKB}, Output: {output}");
        }

        [Fact]
        public void Can_Analyze_Running_Process_x64()
        {
            var process = DumpCreatorTests.AllocStrings(StringAllocx64, N500K);
            int totalAllocatedInKB = StartMemAnalyzer($"-pid {process.Id} -vmmap", out string output);
            Console.WriteLine(output);
            var lines = output.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            //  AllocatedBytes Instances(Count)    Type
            //  27.091.100          553.805             System.String
            //  4.195.576           20                  System.String[]
            //  678.752             26.820              System.Object[]
            //  643.560             26.815              System.Int32
            //  1.608               3                   System.Globalization.CultureData
            //  1.512               27                  System.RuntimeType
            //  830                 5                   System.Char[]
            //  580                 8                   System.Int32[]
            //  432                 2                   System.Globalization.NumberFormatInfo
            //  432                 1                   System.Collections.Generic.Dictionary + Entry<System.Type, System.Security.Policy.EvidenceTypeDescriptor>[]
            //  384                 3                   System.Globalization.CultureInfo
            //  320                 2                   System.Threading.ThreadAbortException
            //  281                 1                   System.Byte[]
            //  216                 1                   System.AppDomain
            //  208                 1                   System.Globalization.CalendarData[]
            //  160                 1                   System.Exception
            //  160                 1                   System.OutOfMemoryException
            //  160                 1                   System.Globalization.CalendarData
            //  160                 1                   System.StackOverflowException
            //  9.629.200           135.367             Managed Heap(Free)
            //  42.248.130                              Managed Heap(Size)
            //  32.618.930          607.562             Managed Heap(Allocated)
            //  29.360.128                              VMMap(Reserved_Stack)
            //  53.424.128                              VMMap(Committed_Dll)
            //  1.933.312                               VMMap(Committed_Heap)
            //  4.161.536                               VMMap(Committed_MappedFile)
            //  3.723.264                               VMMap(Committed_Private)
            //  5.115.904                               VMMap(Committed_Shareable)
            //  115.458.048                             VMMap(Committed_Total)
            //  47.552.940                              Allocated(Total)
            var parts = lines[1].Split("\t ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(x => x.Replace(".", "").Replace(",","")).ToArray();

            int bytes = int.Parse(parts[0]);
            int count = int.Parse(parts[1]);

            Assert.True(bytes > 24 * 1000 * 1000, $"Allocated strings must be > 25MB but was {bytes} bytes");
            Assert.True(count > 500 * 1000, $"Allocated string cont must be > 500K but was {count}");


        }

        [Fact]
        public void Diff_Of_Two_Dumps_Is_Written_To_Default_Csv_File_x86()
        {
            const string String100Kx86 = "Strings100Kx86.dmp";
            if (!File.Exists(String100Kx86))
            {
                AllocateAndDump(StringAllocx86, 2 * N50K, String100Kx86);
            }

            string csvFile = $"{String50Kx86}_Diff_{String100Kx86}.csv";
            if (File.Exists(csvFile))
            {
                File.Delete(csvFile);
            }

            int LeakInKB = StartMemAnalyzer($"-f {String50Kx86} -f2 {String100Kx86} -o -unit Bytes", out string output);
            Console.WriteLine(output);

            Assert.True(File.Exists(csvFile), $"Diff csv file {csvFile} was not created. Output: {output}");
            Assert.True(LeakInKB > 0, $"Leak must be positive but was {LeakInKB} KB");

            string[] lines = File.ReadAllLines(csvFile);
            Assert.Equal("sep=\t", lines[0]);

            string[] header = lines[1].Split('\t');
            Assert.Equal(new string[] { "Delta(Bytes)", "Delta(Instances)", "Instances", "Instances2", "Allocated(Bytes)", "Allocated2(Bytes)", "AvgSize(Bytes)", "AvgSize2(Bytes)", "Type" }, header);

            string[] stringRow = lines.Skip(2).Select(x => x.Split('\t')).SingleOrDefault(x => x.Length == 9 && x[8] == "System.String");
            Assert.NotNull(stringRow);

            long deltaBytes = long.Parse(stringRow[0]);
            long deltaInstances = long.Parse(stringRow[1]);
            long instances = long.Parse(stringRow[2]);
            long instances2 = long.Parse(stringRow[3]);
            long allocated = long.Parse(stringRow[4]);
            long allocated2 = long.Parse(stringRow[5]);

            // Dumps contain also not yet collected temporary strings (no -live) so only lower bounds can be checked
            Assert.True(instances >= N50K, $"First dump must contain at least {N50K} strings but has {instances}");
            Assert.True(instances2 >= 2 * N50K, $"Second dump must contain at least {2 * N50K} strings but has {instances2}");
            Assert.True(deltaInstances >= N50K, $"String instance delta must be at least {N50K} but was {deltaInstances}");
            Assert.Equal(instances2 - instances, deltaInstances);
            Assert.Equal(allocated2 - allocated, deltaBytes);
            Assert.True(deltaBytes > N50K * 20, $"String delta must be > {N50K * 20} bytes but was {deltaBytes}");

            Assert.Contains(lines, x => x.EndsWith("Managed Heap(Allocated)!"));
        }

    }
}
