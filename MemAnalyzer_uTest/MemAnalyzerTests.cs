using MemAnalyzer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemAnalyzer_uTest
{
    [TestClass]
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


        [TestInitialize]
        public void CreateDumpFilesx64AndX86_StringAllocatorMustBeAlreadyCompiledInx64andAd86ToWork()
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
            Assert.IsNotNull(dumpfile, $"Dump {dumpFile} could not be created");
            process.Kill();
        }


        /// <summary>
        /// Start MemoryAnalyzer.exe and return exit code and output
        /// </summary>
        /// <param name="args"></param>
        /// <param name="output"></param>
        /// <returns></returns>
        int StartMemAnalyzer(string args, out string output)
        {
            string dir = Program.GetToolDeployDirectory();
            // delete old baseline which is deployed there. Otherwise we might test for x64 targets 
            // an old version.
            if( Directory.Exists(dir))
            {
                foreach(var file in Directory.GetFiles(dir, "*.*"))
                {
                    File.Delete(file);
                }
            }

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



        [TestMethod]
        public void Can_Compare_Running_Process_With_Dump()
        {
            var process = DumpCreatorTests.AllocStrings(StringAllocx86, N50K);
            int LeakInKB = StartMemAnalyzer($"-pid {process.Id} -f2 {EmptyDumpx86}", out string output);
            Console.WriteLine(output);
            Assert.IsTrue(LeakInKB < -2500, $"Leak should be at least -2500 KB but was {LeakInKB}, Output: {output}");
            
        }

        [TestMethod]
        public void Can_Compare_Two_Dumps_x86()
        {
            int LeakInKB = StartMemAnalyzer($"-f {EmptyDumpx86} -f2 {EmptyDumpx86}", out string output);
            Console.WriteLine(output);
            Assert.AreEqual(0, LeakInKB, "Two same dumps must have same leak size");
            
        }

        [TestMethod]
        public void Leak_Of_Two_Dumps_Is_Deteced_x86()
        {
            int LeakInKB = StartMemAnalyzer($"-f {EmptyDumpx86} -f2 {String50Kx86}", out string output);
            Console.WriteLine(output);
            Assert.IsTrue(LeakInKB > 2500, $"Leak should be at least 2500 KB but was {LeakInKB}, Output: {output}");
            
        }

        [TestMethod]
        public void Leak_Of_Two_Dumps_Is_Deteced_x64()
        {
            int LeakInKB = StartMemAnalyzer($"-f {EmptyDumpx64} -f2 {String50Kx64}", out string output);
            Console.WriteLine(output);
            Assert.IsTrue(LeakInKB > 2500, $"Leak should be at least 2500 KB but was {LeakInKB}, Output: {output}");
        }

        [TestMethod]
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

            Assert.IsTrue(bytes > 24 * 1000 * 1000, $"Allocated strings must be > 25MB but was {bytes} bytes");
            Assert.IsTrue(count > 500 * 1000, $"Allocated string cont must be > 500K but was {count}");
            

        }

    }
}
