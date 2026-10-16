using MemAnalyzer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace MemAnalyzer_uTest
{
    public class CSVReaderTests
    {
        const string devenvLive = @"Dumps\devenv_DumpHeap_Live.csv";
        const string devenvWithVMMap = @"Dumps\devenv_DumpHeap_Live_WithVMMap.csv";

        [Fact]
        public void ParseVSFile()
        {
            CSVReader reader = new CSVReader(devenvLive, null);
            var data = reader.Parse();

            var first = Assert.Single(data);

            Assert.Equal(20002, first.Key.Count);
            Assert.Null(first.Value);

            var stringType = first.Key["System.String"];

            Assert.Equal(12061512, stringType.AllocatedSizeInBytes);
            Assert.Equal(145837, stringType.Count);
            Assert.Equal(5824, stringType.ProcessContext.Pid);
            Assert.Equal(new DateTime(2017, 10, 3, 22, 35, 50, 0, DateTimeKind.Local), stringType.ProcessContext.Time);
            Assert.Equal("devenv", stringType.ProcessContext.Name);
            Assert.Equal(2388, stringType.ProcessContext.AgeIns);
            Assert.Equal(@"C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\IDE\devenv.exe", stringType.ProcessContext.CommandLine);

        }
    }
}
