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

            Assert.AreEqual(72124, data.Reserved_MappedFileBytes);
            Assert.AreEqual(72124, data.Committed_MappedFileBytes);
        }

        [TestMethod]
        public void CanParseCSV()
        {
            VMMap map = new VMMap(100);
            File.Copy("Dumps\\VMMAP.csv", "test.csv", true);
            VMMapData lret = map.ParseVMMapFile("test.csv");
        }
    }
}
