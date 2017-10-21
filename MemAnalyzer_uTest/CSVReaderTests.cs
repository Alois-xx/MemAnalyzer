using MemAnalyzer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemAnalyzer_uTest
{
    [TestClass]
    public class CSVReaderTests
    {
        const string devenvLive = @"Dumps\devenv_DumpHeap_Live.csv";
        const string devenvWithVMMap = @"Dumps\devenv_DumpHeap_Live_WithVMMap.csv";

        [TestMethod]
        public void ParseVSFile()
        {
            CSVReader reader = new CSVReader(devenvLive, null);
            var data = reader.Parse();
            Assert.AreEqual(1, data.Count);

            var first = data[0];

            Assert.AreEqual(20002, first.Key.Count);
            Assert.AreEqual(null, first.Value);

            var stringType = first.Key["System.String"];

            Assert.AreEqual(12061512, stringType.AllocatedSizeInBytes);
            Assert.AreEqual(145837, stringType.Count);
            Assert.AreEqual(5824, stringType.ProcessContext.Pid);
            Assert.AreEqual(new DateTime(2017, 10, 3, 22, 35, 50, 0, DateTimeKind.Local), stringType.ProcessContext.Time);
            Assert.AreEqual("devenv", stringType.ProcessContext.Name);
            Assert.AreEqual(2388, stringType.ProcessContext.AgeIns);
            Assert.AreEqual(@"C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\IDE\devenv.exe", stringType.ProcessContext.CommandLine);

        }
    }
}
