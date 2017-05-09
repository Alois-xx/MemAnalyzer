using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MemAnalyzer
{
    class VMMap
    {
        const string VmMap = "vmmap.exe";
        const string SummaryDataStart = "\"Type\",\"Size\",\"Committed\"";
        const string SummaryDataEnd = "\"Address\",\"Type\",\"Size\"";
        static readonly char[] SplitChars = new char[] { ',' };

        Dictionary<string, Action<long, long, VMMapData>> RowMapper =  new Dictionary<string, Action<long, long, VMMapData>>
        {
            { "Image", (col1, col2, data) =>  { data.Reserved_DllBytes = col1;  data.Committed_DllBytes = col2; } },
            { "Mapped File", (col1, col2, data) =>  { data.Reserved_MappedFileBytes = col1;  data.Committed_MappedFileBytes = col2; }},
            { "Shareable", (col1, col2, data) =>  { data.Reserved_ShareableBytes = col1; data.Committed_ShareableBytes = col2; } },
            { "Heap", (col1, col2, data) =>  { data.Reserved_HeapBytes = col1; data.Committed_HeapBytes = col2; } },
            { "Managed Heap", (col1, col2, data) =>  { data.Reserved_ManagedHeapBytes = col1; data.Committed_ManagedHeapBytes = col2; } },
            { "Stack", (col1, col2, data) =>  { data.Reserved_Stack = col1; data.Committed_Stack = col2; } },
            { "Private Data", (col1, col2, data) =>  { data.Reserved_PrivateBytes = col1; data.Committed_PrivateBytes = col2; } },
            { "Page Table", (col1, col2, data) =>  { data.Reserved_PageTable = col1; data.Committed_PageTable = col2; } },
        };

        string TempFileName;
        int Pid;
        static bool NoVMMap = false;

        public VMMap(int pid)
        {
            if( pid <= 0 )
            {
                throw new ArgumentNullException("pid");
            }
            Pid = pid;
            TempFileName = Path.Combine(Path.GetTempPath(), $"VMMapData_{pid}.csv");
        }

        public VMMapData GetMappingData()
        {
            if (NoVMMap)
            {
                return new VMMapData();
            }

            var info = new ProcessStartInfo(VmMap, $"-p {Pid} {TempFileName}")
            {
                CreateNoWindow = true,
            };

            try
            {
                var p = Process.Start(info);
                p.WaitForExit();
            }
            catch (Exception ex)
            {
                NoVMMap = true; // do not try again if not present.
                DebugPrinter.Write($"Could not start VMMap: {info.FileName} {info.Arguments}. Got: {ex}");
            }

            VMMapData lret = ParseVMMapFile(TempFileName);
            return lret;
        }

        static internal string[] SplitLine(string line)
        {
            bool inToken = false;
            int startIdx = 0;

            List<string> parts = new List<string>();

            for(int i=0;i<line.Length;i++)
            {
                if( line[i] == '"')
                {
                    inToken = !inToken;
                    if( inToken )
                    {
                        startIdx = i;
                    }
                    else
                    {
                        int len = i - startIdx - 1;
                        // replace thousands separator in every language
                        parts.Add(line.Substring(startIdx + 1, len).Replace(",", "").Replace(".",""));
                    }
                }
            }

            return parts.ToArray();
        }

        internal VMMapData ParseVMMapFile(string fileName)
        {
            VMMapData lret = new VMMapData();
            if (!File.Exists(fileName))
            {
                return lret;
            }

            using (var reader = new StreamReader(fileName))
            {
                string line = null;
                bool bShouldParse = false;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith(SummaryDataStart))
                    {
                        bShouldParse = true;
                        continue;
                    }
                    if (line.StartsWith(SummaryDataStart))
                    {
                        bShouldParse = false;
                        break;
                    }

                    if (!bShouldParse)
                    {
                        continue;
                    }

                    MapDataFromLine(lret, line);
                }
            }

            RemoveVMMapFile();

            return lret;
        }

        internal void MapDataFromLine(VMMapData lret, string line)
        {
            string[] parts = SplitLine(line);
            if (parts.Length >= 2)
            {
                string name = parts[0];
                if (RowMapper.TryGetValue(parts[0], out Action<long, long, VMMapData> mapper))
                {
                    long reserved = long.Parse(parts[1]);
                    long committed = long.Parse(parts[2]);
                    mapper(reserved, committed, lret);
                }
            }
        }

        private void RemoveVMMapFile()
        {
            File.Delete(TempFileName);
        }
    }
}
