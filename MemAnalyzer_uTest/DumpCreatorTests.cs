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
    public class DumpCreatorTests
    {
        string exe = "StringAllocator.exe";
        string dumpFileName = "test.dmp";

        [TestInitialize]
        public void CleanDump()
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

        [TestMethod]
        public void Can_Dump_Process()
        {
            DumpCreator creator = new DumpCreator(true);
            var process = AllocStrings();

            string file = creator.Dump(new string[] { process.Id.ToString(), "test.dmp" });
            Assert.IsNotNull(file, "Output dump file was null!");
            Assert.IsTrue(File.Exists(file), "Dump file does not exist!");
            Assert.IsNotNull(TargetInformation.GetExistingVMMapFile(file), "Associated VMMap dump file was not found. Is VMMap.exe in path?");
        }

        [TestMethod]
        public void Can_Parse_RegularPid()
        {
            string[] args = new string[] { "-ma", "5000" };
            int pid = DumpCreator.FindPidInProcDumpArgs(args, out string exeName);
            Assert.AreEqual(5000, pid);
        }

        [TestMethod]
        public void IfExeAndDumpFile_Present_Ignore_Dump()
        {
            string[] args = new string[] { "-ma", "notepad", "notepad.dmp" };
            int pid = DumpCreator.FindPidInProcDumpArgs(args, out string exeName);
            Assert.AreEqual("notepad", exeName);
        }

        [TestMethod]
        public void ExeFilePresent_DumpFileNot()
        {
            string[] args = new string[] { "-ma", "notepad" };
            int pid = DumpCreator.FindPidInProcDumpArgs(args, out string exeName);
            Assert.AreEqual("notepad", exeName);
        }


        [TestMethod]
        public void Can_Parse_RegularexeName()
        {
            string[] args = new string[] { "-ma", "notepad.exe" };
            int pid = DumpCreator.FindPidInProcDumpArgs(args, out string exeName);
            Assert.AreEqual(0, pid);
            Assert.AreEqual("notepad.exe", exeName);
        }

        [TestMethod]
        public void Can_Parse_RegularexeNameSecondLast()
        {
            string[] args = new string[] { "notepad.exe", "-ma" };
            int pid = DumpCreator.FindPidInProcDumpArgs(args, out string exeName);
            Assert.AreEqual(0, pid);
            Assert.AreEqual("notepad.exe", exeName);
        }



        [TestMethod]
        public void Can_Parse_SinglePidArg()
        {
            string[] args = new string[] { "5000" };
            int pid = DumpCreator.FindPidInProcDumpArgs(args, out string exeName);
            Assert.AreEqual(5000, pid);
        }


        Process AllocStrings(int n=0)
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
