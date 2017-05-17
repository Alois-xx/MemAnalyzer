using MemAnalyzer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemAnalyzer_uTest
{
    [TestClass]
    public class VMMapTests
    {
        /// <summary>
        /// VMMap reports its number in KB!
        /// </summary>
        const string Line = "\"Mapped File\",\"72,124\",\"72,124\",\"\",\"368\",\"\",\"368\",\"248\",\"\",\"86\",\"17,216\"";


        [TestMethod]
        public void Can_Match_Parts()
        {
            var matches = VMMap.SplitLine(Line);
            Assert.AreEqual(11, matches.Length);
            Assert.AreEqual("Mapped File", matches[0]);
            Assert.AreEqual("72124", matches[1]);
            Assert.AreEqual("72124", matches[2]);
            Assert.AreEqual("", matches[3]);
            Assert.AreEqual("368", matches[4]);
            Assert.AreEqual("", matches[5]);
            Assert.AreEqual("368", matches[6]);
            Assert.AreEqual("248", matches[7]);
            Assert.AreEqual("", matches[8]);
            Assert.AreEqual("86", matches[9]);
            Assert.AreEqual("17216", matches[10]);
        }

        [TestMethod]
        public void CanMapLineData()
        {
            VMMap map = new VMMap(100);
            VMMapData data = new VMMapData();

            map.MapDataFromLine(data, Line);

            Assert.AreEqual(1024*72124, data.Reserved_MappedFileBytes);
            Assert.AreEqual(1024*72124, data.Committed_MappedFileBytes);
        }

        [TestMethod]
        public void CanParseCSV()
        {
            VMMap map = new VMMap(100);
            string tempVMMapFile = "test.csv";
            File.Copy("Dumps\\VMMAP.csv", tempVMMapFile, true);
            Assert.IsTrue(File.Exists(tempVMMapFile));
            VMMapData lret = map.ParseVMMapFile("test.csv", bDelete:true);
            Assert.IsFalse(File.Exists(tempVMMapFile));
            Assert.AreEqual(1024*586096, lret.Reserved_DllBytes);
            Assert.AreEqual(1024*583724, lret.Committed_DllBytes);
            Assert.AreEqual(1024*132224, lret.Reserved_ManagedHeapBytes);
            Assert.AreEqual(1024*108200, lret.Committed_ManagedHeapBytes);
            Assert.AreEqual(1024*87464, lret.Reserved_HeapBytes);
            Assert.AreEqual(1024*72356, lret.Committed_HeapBytes);
            Assert.AreEqual(1024*72124, lret.Reserved_MappedFileBytes);
            Assert.AreEqual(1024*72124, lret.Committed_MappedFileBytes);
            Assert.AreEqual(1024*26952, lret.Reserved_ShareableBytes);
            Assert.AreEqual(1024*6396, lret.Committed_ShareableBytes);
            Assert.AreEqual(1024*40704, lret.Reserved_Stack);
            Assert.AreEqual(1024*4192, lret.Committed_Stack);
            Assert.AreEqual(1024*91736, lret.Reserved_PrivateBytes);
            Assert.AreEqual(1024*19220, lret.Committed_PrivateBytes);
            Assert.AreEqual(1024*32368, lret.Reserved_PageTable);
            Assert.AreEqual(1024*32368, lret.Committed_PageTable);
            Assert.AreEqual(1024*2083712, lret.LargestFreeBlockBytes);
        }

        [TestMethod]
        public void Can_Parse_ExistingFile_And_Do_Not_Delete_It_After_Parse()
        {
            string tempVMMapFile = "test.csv";
            File.Copy("Dumps\\VMMAP.csv", tempVMMapFile, true);

            VMMap map = new VMMap(tempVMMapFile);

            Assert.IsTrue(File.Exists(tempVMMapFile));

            VMMapData lret = map.GetMappingData();

            Assert.IsTrue(File.Exists(tempVMMapFile));

            File.Delete(tempVMMapFile);

            Assert.AreEqual(true, lret.HasValues);
            Assert.AreEqual(1024 * 2083712, lret.LargestFreeBlockBytes);
        }
    }
}
