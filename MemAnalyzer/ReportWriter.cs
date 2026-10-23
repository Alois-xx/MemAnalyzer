using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MemAnalyzer
{
    /// <summary>
    /// Writes the memory report lines (size, instance count, type name) to the console or to the CSV output.
    /// For CSV output the process columns (ProcessId, Time, CommandLine, Age(s), Name, Context) are appended so
    /// that snapshots from many runs can be appended into one CSV file and can be diffed later.
    /// It is used by the analyzers and by -procdump so that both write the identical report format.
    /// </summary>
    internal class ReportWriter
    {
        // Default format string for console output.
        // fmt is ignored for CSV output where all columns are simply \t separated.
        const string fmt = "{0,-17:N0}\t{1,-17:N0}\t{2}";

        TargetInformation TargetInfo;
        DisplayUnit DisplayUnit;
        string TimeAndOrDate;
        string CmdLine;
        string Context;
        bool IsFirstLine = true;

        /// <summary>
        /// Write one report line for the given target.
        /// </summary>
        /// <param name="info">Target process pid and/or dump file name.</param>
        /// <param name="displayunit">Unit in which bytes are displayed.</param>
        /// <param name="timeFormat">.NET format string for time and date. If the value is Invariant the invariant culture is used.</param>
        /// <param name="context">Optional context column which is written for CSV output.</param>
        public ReportWriter(TargetInformation info, DisplayUnit displayunit, string timeFormat = null, string context = null)
        {
            TargetInfo = info;
            DisplayUnit = displayunit;
            Context = context;
            DateTime now = TargetInfo.CurrentTimeOrDumpCreationDate;
            TimeAndOrDate = TargetInfo.ExternalTime ?? (timeFormat == "Invariant" ? now.ToString(CultureInfo.InvariantCulture) : now.ToString(timeFormat));
            CmdLine = TargetInfo.IsLiveProcess ? TargetInformation.GetProcessCmdLine(TargetInfo.Pid1) : "";
        }

        /// <summary>
        /// Write one report line. The process columns are only written when CSV output is active.
        /// </summary>
        /// <param name="allocated">Allocated bytes</param>
        /// <param name="instances">Allocated instances</param>
        /// <param name="type">Type name</param>
        public void WriteLine(long allocated, long instances, string type)
        {
            if (IsFirstLine)
            {
                string[] header = new string[] { MemAnalyzer.AllocatedColumnBase + DisplayUnit, MemAnalyzer.InstancesColumn, MemAnalyzer.TypeColumn };

                if (OutputStringWriter.CsvOutput)
                {
                    header = header.Concat(new string[] { MemAnalyzer.ProcessIdColumn, MemAnalyzer.TimeColumn, MemAnalyzer.CommandeLineColumn, MemAnalyzer.AgeColumn, MemAnalyzer.NameColumn, MemAnalyzer.ContextColumn }).ToArray();
                }

                // write header
                OutputStringWriter.FormatAndWriteHeader(fmt, header);
                IsFirstLine = false;
            }

            if (OutputStringWriter.CsvOutput)
            {
                OutputStringWriter.FormatAndWrite(fmt, allocated, instances, type, TargetInfo.IsLiveProcess ? TargetInfo.Pid1.ToString() : "", TimeAndOrDate, CmdLine, TargetInfo.ProcessAgeInSeconds, TargetInfo.ProcessName, Context);
            }
            else
            {
                OutputStringWriter.FormatAndWrite(fmt, allocated, instances == 0 ? "" : (object)instances, type);
            }
        }

        /// <summary>
        /// Format a type with no instance count.
        /// </summary>
        /// <returns>Delegate which formats the type and its size in the respective unit.</returns>
        public Action<string, long> GetSimpleTypeFormatter()
        {
            return (typeName, sizeInBytes) => WriteLine(sizeInBytes / (long)DisplayUnit, 0, typeName);
        }

        /// <summary>
        /// Write the VMMap rows of a snapshot with the given formatter function.
        /// </summary>
        public void WriteVMMapData(VMMapData vm)
        {
            Action<string, long> formatter = GetSimpleTypeFormatter();

            // free blocks only play a role in x86
            if (vm.LargestFreeBlockBytes < 4 * 1024 * 1024 * 1024L)
            {
                formatter(VMMapData.Col_Reserved_LargestFreeBlock, vm.LargestFreeBlockBytes);
            }
            formatter(VMMapData.Col_Reserved_Stack, vm.Reserved_Stack);
            formatter(VMMapData.Col_Committed_Dll, vm.Committed_DllBytes);
            formatter(VMMapData.Col_Committed_Heap, vm.Committed_HeapBytes);
            formatter(VMMapData.Col_Committed_MappedFile, vm.Committed_MappedFileBytes);
            formatter(VMMapData.Col_Committed_Private, vm.Committed_PrivateBytes);
            formatter(VMMapData.Col_Committed_Shareable, vm.Committed_ShareableBytes);
            formatter(VMMapData.Col_Committed_Total, vm.TotalCommittedBytes);
        }
    }
}
