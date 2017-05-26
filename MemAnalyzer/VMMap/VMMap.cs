using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MemAnalyzer
{
    class VMMap
    {
        const string VmMap = "vmmap.exe";
        const string SummaryDataStart = "\"Type\",\"Size\",\"Committed\"";
        const string SummaryDataEnd = "\"Address\",\"Type\",\"Size\"";
        static readonly char[] SplitChars = new char[] { ',' };

        Dictionary<string, Action<long, long, long, VMMapData>> RowMapper =  new Dictionary<string, Action<long, long, long, VMMapData>>
        {
            { "Image", (col1, col2, _,  data) =>          { data.Reserved_DllBytes = col1*1024L;         data.Committed_DllBytes = col2*1024L; } },
            { "Mapped File", (col1, col2, _, data) =>     { data.Reserved_MappedFileBytes = col1*1024L;  data.Committed_MappedFileBytes = col2*1024L; }},
            { "Shareable", (col1, col2, _, data) =>       { data.Reserved_ShareableBytes = col1*1024L;   data.Committed_ShareableBytes = col2*1024L; } },
            { "Heap", (col1, col2, _, data) =>            { data.Reserved_HeapBytes = col1*1024L;        data.Committed_HeapBytes = col2*1024L; } },
            { "Managed Heap", (col1, col2, _, data) =>    { data.Reserved_ManagedHeapBytes = col1*1024L; data.Committed_ManagedHeapBytes = col2*1024L; } },
            { "Stack", (col1, col2, _, data) =>           { data.Reserved_Stack = col1*1024L;            data.Committed_Stack = col2*1024L; } },
            { "Private Data", (col1, col2, _, data) =>    { data.Reserved_PrivateBytes = col1*1024L;     data.Committed_PrivateBytes = col2*1024L; } },
            { "Page Table", (col1, col2, _, data) =>      { data.Reserved_PageTable = col1*1024L;        data.Committed_PageTable = col2*1024L; } },
            { "Free", ( _, __, largestFreeBlock, data) => { data.LargestFreeBlockBytes= largestFreeBlock*1024L; } },
        };

        string TempFileName;
        int Pid;

        static bool NoVMMap = false;

        string ExistingVMMapFile;

        /// <summary>
        /// Get VMMap information from a running process.
        /// </summary>
        /// <param name="pid"></param>
        public VMMap(int pid)
        {
            if( pid <= 0 )
            {
                throw new ArgumentNullException("pid");
            }
            Pid = pid;
            TempFileName = Path.Combine(Path.GetTempPath(), $"VMMapData_{pid}.csv");
        }

        /// <summary>
        /// Read previously recorded VMMap mapping data from a file.
        /// </summary>
        /// <param name="vMMapFile"></param>
        public VMMap(string vMMapFile)
        {
            if( String.IsNullOrEmpty(vMMapFile) )
            {
                throw new ArgumentNullException("vMMapFile");
            }
            ExistingVMMapFile = vMMapFile;
        }

        /// <summary>
        /// Parse VMMap output from an existing process or a previously saved csv file.
        /// </summary>
        /// <returns>Parsed VMMap data. If an error occurs a empty VMMapData instance is returned.</returns>
        public VMMapData GetMappingData()
        {
            if (ExistingVMMapFile != null)
            {
                return ParseVMMapFile(ExistingVMMapFile, bDelete: false);
            }

            if (NoVMMap)
            {
                return new VMMapData();
            }

            SaveVMmapDataToFile(Pid, TempFileName);
            VMMapData lret = ParseVMMapFile(TempFileName, bDelete: true);

            return lret;
        }

        /// <summary>
        /// Save VMMap data to output file
        /// </summary>
        /// <param name="pid"></param>
        /// <param name="outFile"></param>
        public static void SaveVMmapDataToFile(int pid, string outFile)
        {
            var info = new ProcessStartInfo(VmMap, $"-p {pid} {outFile}")
            {
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            try
            {
                var p = Process.Start(info);
                p.WaitForExit();

                // Looks like a bug in VMMap where the x64 child process is still writing the output data but the
                // x86 parent process has already exited.
                for (int i = 0; i < 10 && !File.Exists(outFile); i++)
                {
                    DebugPrinter.Write($"VMMap has exited but output file {outFile} does not yet exist");
                    Thread.Sleep(2000);
                }
            }
            catch (Exception ex)
            {
                NoVMMap = true; // do not try again if not present.
                DebugPrinter.Write($"Could not start VMMap: {info.FileName} {info.Arguments}. Got: {ex}");
            }
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

        internal VMMapData ParseVMMapFile(string fileName, bool bDelete)
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

            if (bDelete)
            {
                RemoveTempVMMapFile(fileName);
            }

            return lret;
        }

        internal void MapDataFromLine(VMMapData lret, string line)
        {
            string[] parts = SplitLine(line);
            if (parts.Length >= 11)
            {
                string name = parts[0];
                if (RowMapper.TryGetValue(parts[0], out Action<long, long, long, VMMapData> mapper))
                {
                    if( parts[1] == "") // Page table data can sometimes be empty
                    {
                        return;
                    }
                    long reserved = long.Parse(parts[1]);
                    long.TryParse(parts[2], out long committed);
                    long.TryParse(parts[10], out long largestBlock);
                    mapper(reserved, committed, largestBlock, lret);
                }
            }
        }

        private void RemoveTempVMMapFile(string file)
        {
            if (file != null && File.Exists(file))
            {
                File.Delete(file);
            }
        }
    }
}
