using MemAnalyzer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace MemAnalyzer_uTest
{
    public class VMMapTests
    {
        /// <summary>
        /// VMMap reports its number in KB!
        /// </summary>
        const string Line = "\"Mapped File\",\"72,124\",\"72,124\",\"\",\"368\",\"\",\"368\",\"248\",\"\",\"86\",\"17,216\"";


        [Fact]
        public void Can_Match_Parts()
        {
            var matches = VMMap.SplitLine(Line);
            Assert.Equal(11, matches.Length);
            Assert.Equal("Mapped File", matches[0]);
            Assert.Equal("72124", matches[1]);
            Assert.Equal("72124", matches[2]);
            Assert.Equal("", matches[3]);
            Assert.Equal("368", matches[4]);
            Assert.Equal("", matches[5]);
            Assert.Equal("368", matches[6]);
            Assert.Equal("248", matches[7]);
            Assert.Equal("", matches[8]);
            Assert.Equal("86", matches[9]);
            Assert.Equal("17216", matches[10]);
        }

        [Fact]
        public void CanMapLineData()
        {
            VMMap map = new VMMap(100);
            VMMapData data = new VMMapData();

            map.MapDataFromLine(data, Line);

            Assert.Equal(1024*72124, data.Reserved_MappedFileBytes);
            Assert.Equal(1024*72124, data.Committed_MappedFileBytes);
        }

        [Fact]
        public void CanParseCSV()
        {
            VMMap map = new VMMap(100);
            string tempVMMapFile = "test.csv";
            File.Copy("Dumps\\VMMAP.csv", tempVMMapFile, true);
            Assert.True(File.Exists(tempVMMapFile));
            VMMapData lret = map.ParseVMMapFile("test.csv", bDelete:true);
            Assert.False(File.Exists(tempVMMapFile));
            Assert.Equal(1024*586096, lret.Reserved_DllBytes);
            Assert.Equal(1024*583724, lret.Committed_DllBytes);
            Assert.Equal(1024*132224, lret.Reserved_ManagedHeapBytes);
            Assert.Equal(1024*108200, lret.Committed_ManagedHeapBytes);
            Assert.Equal(1024*87464, lret.Reserved_HeapBytes);
            Assert.Equal(1024*72356, lret.Committed_HeapBytes);
            Assert.Equal(1024*72124, lret.Reserved_MappedFileBytes);
            Assert.Equal(1024*72124, lret.Committed_MappedFileBytes);
            Assert.Equal(1024*26952, lret.Reserved_ShareableBytes);
            Assert.Equal(1024*6396, lret.Committed_ShareableBytes);
            Assert.Equal(1024*40704, lret.Reserved_Stack);
            Assert.Equal(1024*4192, lret.Committed_Stack);
            Assert.Equal(1024*91736, lret.Reserved_PrivateBytes);
            Assert.Equal(1024*19220, lret.Committed_PrivateBytes);
            Assert.Equal(1024*32368, lret.Reserved_PageTable);
            Assert.Equal(1024*32368, lret.Committed_PageTable);
            Assert.Equal(1024*2083712, lret.LargestFreeBlockBytes);
        }

        [Fact]
        public void Can_Parse_ExistingFile_And_Do_Not_Delete_It_After_Parse()
        {
            string tempVMMapFile = "test.csv";
            File.Copy("Dumps\\VMMAP.csv", tempVMMapFile, true);

            VMMap map = new VMMap(tempVMMapFile);

            Assert.True(File.Exists(tempVMMapFile));

            VMMapData lret = map.GetMappingData();

            Assert.True(File.Exists(tempVMMapFile));

            File.Delete(tempVMMapFile);

            Assert.True(lret.HasValues);
            Assert.Equal(1024 * 2083712, lret.LargestFreeBlockBytes);
        }
    }
}
