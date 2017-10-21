using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemAnalyzer
{
    /// <summary>
    /// CSV Parser to read CSV files back in
    /// </summary>
    internal class CSVReader
    {
        string _File;

        char[] Seps = new char[] { OutputStringWriter.SeparatorChar };

        string[] ColumnNames = null;


        /// <summary>
        /// This contains the delegates which fill for each column the corresponding RowData properties. The list is created
        /// when the CSV column header was read to identify which columns are in which position.
        /// </summary>
        List<Action<RowData, string>> Parsers = new List<Action<RowData, string>>();

        //AllocatedBytes Instances(Count)    Type ProcessId   Time CommandLine Age(s)  Name Context
        //12061512	145837	System.String	5824	10/3/2017 10:35:50 PM C:\Program Files(x86)\Microsoft Visual Studio\2017\Enterprise\Common7\IDE\devenv.exe  	2388	devenv

        const string SepLine = "sep=";

        static HashSet<string> VMMapRows = new HashSet<string>
        {
            VMMapData.Col_Reserved_LargestFreeBlock,
            VMMapData.Col_Reserved_Stack,
            VMMapData.Col_Committed_Dll,
            VMMapData.Col_Committed_Heap,
            VMMapData.Col_Committed_MappedFile,
            VMMapData.Col_Committed_Private,
            VMMapData.Col_Committed_Shareable,
            VMMapData.Col_Committed_Total
        };

        static HashSet<string> TotalRows = new HashSet<string>()
        {
            MemAnalyzer.ManagedHeapSize,
            MemAnalyzer.ManagedHeapFree,
            MemAnalyzer.ManagedHeapAllocated,
            MemAnalyzer.AllocatedTotal,
        };

        static Dictionary<string, Func<string, long>> LongColumnParsers = new Dictionary<string, Func<string, long>>
        {
            [MemAnalyzer.AllocatedBytesColumn]  = str => long.Parse(str),
            [MemAnalyzer.AllocatedKBColumn]     = str => long.Parse(str) / 1024L,
            [MemAnalyzer.AllocatedMBColumn]     = str => long.Parse(str) / (1024*1024L),
            [MemAnalyzer.AllocatedGBColumn]     = str => long.Parse(str) / (1024L*1024*1024),
            [MemAnalyzer.InstancesColumn]       = str => long.Parse(str)
        };

        static Dictionary<string, Func<string, string>> StringColumnParsers = new Dictionary<string, Func<string, string>>
        {
            [MemAnalyzer.TypeColumn]         = str => str,
            [MemAnalyzer.CommandeLineColumn] = str => str.Trim(),
            [MemAnalyzer.NameColumn]         = str => str,
            [MemAnalyzer.ContextColumn]      = str => str,
        };

        static Dictionary<string, Func<string, int>> IntColumnParsers = new Dictionary<string, Func<string, int>>
        {
            [MemAnalyzer.ProcessIdColumn] = int.Parse,
            [MemAnalyzer.AgeColumn] = int.Parse,
        };

        Dictionary<string, Func<string, DateTime>> TimeColumnParsers;

        /// <summary>
        /// A CSV row contains basically type information and many redundant process context infos which are repeated
        /// for all dumped types. There redundant infos are stored in one common instance to spare memory
        /// </summary>
        class RowData
        {
            public long AllocatesBytes;
            public long Instances;
            public string Type;

            public ProcessContextInfo ProcessContext
            {
                get;
                set;
            } = new ProcessContextInfo();
        }

        public CSVReader(string file, string timefmt)
        {
            _File = file;
            Func<string, DateTime> timeParser = str => DateTime.Parse(str, CultureInfo.InvariantCulture);
            if( timefmt != null )
            {
                timeParser = str => DateTime.ParseExact(str, timefmt, CultureInfo.InvariantCulture);
            }

            TimeColumnParsers = new Dictionary<string, Func<string, DateTime>>
            {
                [MemAnalyzer.TimeColumn] = timeParser
            };
        }

        /// <summary>
        /// Parse a CSV file which can contain the type list and vmmap data or a collection of memory snapshot
        /// which were appended to the csv file.
        /// </summary>
        /// <returns>List of memory snapshots</returns>
        public List<KeyValuePair<Dictionary<string, ProcessTypeInfo>,VMMapData>> Parse()
        {
            var lret = new List<KeyValuePair<Dictionary<string, ProcessTypeInfo>, VMMapData>>();
            using (var file = File.OpenText(_File))
            {
                ProcessContextInfo currentProcess = new ProcessContextInfo();
                string line = null;
                bool bFirst = true;
                RowData rowData = new RowData();
                VMMapProcessData currentVMMap = new VMMapProcessData();
                var currentTypeMap = new Dictionary<string, ProcessTypeInfo>();
                while ( (line= file.ReadLine()) != null )
                {
                    if( bFirst )
                    {
                        // get column separator if present
                        if( line.StartsWith(SepLine) && line.Length == SepLine.Length+1)
                        {
                            Seps[0] = line[SepLine.Length]; 
                            continue;
                        }

                        // get column names
                        ColumnNames = line.Split(Seps);
                        CreateParser(ColumnNames);
                        bFirst = false;
                        continue;
                    }

                    // get columns
                    var parts  = line.Split(Seps);

                    // parse CSV columns into intermediate row object 
                    ParseColumns(parts, rowData);

                    if( currentProcess.Pid != rowData.ProcessContext.Pid)
                    {
                        if (currentProcess.Pid != 0)
                        {
                            lret.Add(new KeyValuePair<Dictionary<string, ProcessTypeInfo>, VMMapData>(currentTypeMap, ReturnNullIfNoDataPresent(currentVMMap)));
                            currentTypeMap = new Dictionary<string, ProcessTypeInfo>();
                            currentVMMap = new VMMapProcessData();
                        }
                        currentProcess = rowData.ProcessContext;

                    }

                    if(VMMapRows.Contains(rowData.Type))
                    {
                        currentVMMap.Add(rowData.Type, rowData.AllocatesBytes);
                    }
                    else
                    {
                        currentTypeMap.Add(rowData.Type, new ProcessTypeInfo(currentProcess)
                        {
                            Name = rowData.Type,
                            AllocatedSizeInBytes = rowData.AllocatesBytes,
                            Count = rowData.Instances
                        });
                    }
                }

                lret.Add(new KeyValuePair<Dictionary<string, ProcessTypeInfo>, VMMapData>(currentTypeMap, ReturnNullIfNoDataPresent(currentVMMap)));
            }

            return lret;
        }

        VMMapData ReturnNullIfNoDataPresent(VMMapData vmmap)
        {
            return vmmap.IsEmpty ? null : vmmap;
        }

        /// <summary>
        /// Parse columns into an already existing RowData object
        /// </summary>
        /// <param name="parts">columns row data</param>
        /// <param name="row">Already existing RowData object into which the contents are overwritten</param>
        private void ParseColumns(string[] parts, RowData row)
        {
            for(int i=0;i< Parsers.Count;i++)
            {
                Parsers[i](row, parts[i]);
            }
        }

        private void CreateParser(string[] columnNames)
        {
            for(int i=0;i<columnNames.Length;i++)
            {
                switch(columnNames[i])
                {
                    case MemAnalyzer.AllocatedBytesColumn:
                        Parsers.Add((row, str) => { row.AllocatesBytes = LongColumnParsers[MemAnalyzer.AllocatedBytesColumn](str); });
                        break;
                    case MemAnalyzer.AllocatedKBColumn:
                        Parsers.Add((row, str) => { row.AllocatesBytes = LongColumnParsers[MemAnalyzer.AllocatedKBColumn](str); });
                        break;
                    case MemAnalyzer.AllocatedMBColumn:
                        Parsers.Add((row, str) => { row.AllocatesBytes = LongColumnParsers[MemAnalyzer.AllocatedMBColumn](str); });
                        break;
                    case MemAnalyzer.AllocatedGBColumn:
                        Parsers.Add((row, str) => { row.AllocatesBytes = LongColumnParsers[MemAnalyzer.AllocatedGBColumn](str); });
                        break;
                    case MemAnalyzer.InstancesColumn:
                        Parsers.Add((row, str) => { row.Instances = LongColumnParsers[MemAnalyzer.InstancesColumn](str); });
                        break;
                    case MemAnalyzer.TypeColumn:
                        Parsers.Add((row, str) => { row.Type = StringColumnParsers[MemAnalyzer.TypeColumn](str); });
                        break;
                    case MemAnalyzer.ProcessIdColumn:
                        Parsers.Add((row, str) => { row.ProcessContext.Pid = IntColumnParsers[MemAnalyzer.ProcessIdColumn](str); });
                        break;
                    case MemAnalyzer.AgeColumn:
                        Parsers.Add((row, str) => { row.ProcessContext.AgeIns = IntColumnParsers[MemAnalyzer.AgeColumn](str); });
                        break;
                    case MemAnalyzer.TimeColumn:
                        Parsers.Add((row, str) => { row.ProcessContext.Time = TimeColumnParsers[MemAnalyzer.TimeColumn](str); });
                        break;
                    case MemAnalyzer.CommandeLineColumn:
                        Parsers.Add((row, str) => { row.ProcessContext.CommandLine = StringColumnParsers[MemAnalyzer.CommandeLineColumn](str); });
                        break;
                    case MemAnalyzer.NameColumn:
                        Parsers.Add((row, str) => { row.ProcessContext.Name = StringColumnParsers[MemAnalyzer.NameColumn](str); });
                        break;
                    case MemAnalyzer.ContextColumn:
                        Parsers.Add((row, str) => { row.ProcessContext.Context = StringColumnParsers[MemAnalyzer.ContextColumn](str); });
                        break;

                    default:
                        throw new NotSupportedException($"Column name {columnNames[i]} is not recognized as valid column. Cannot parse further.");
                }
            }
        }
    }
}
