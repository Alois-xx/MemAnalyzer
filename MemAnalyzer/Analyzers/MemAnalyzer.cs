using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemAnalyzer
{
    /// <summary>
    /// Analyzes one or two managed heaps and print out the allocation statistics per type or the differences per type.
    /// </summary>
    class MemAnalyzer : MemAnalyzerBase
    {
        // Magic type names for special Free type name
        const string FreeTypeName = "Free";

        bool GetVMMapData;

        TargetInformation TargetInfo;

        // Additional columns which are printed when CSV output is active
        string ProcessName;
        string CmdLine;
        string TimeAndOrDate;
        string Context;
        bool IsFirstLine = true;


        /// <summary>
        /// Row names in output. Rows with a ! in their name are not aggregated values
        /// which makes it easy to filter for them in Pivot Charts with ! as text filter
        /// </summary>
        const string Reserved_LargestFreeBlock = "Reserved_LargestFreeBlock";
        const string Reserved_Stack = "Reserved_Stack";
        const string Committed_Dll = "Committed_Dll";
        const string Committed_Heap = "Committed_Heap!";
        const string Committed_MappedFile = "Committed_MappedFile!";
        const string Committed_Private = "Committed_Private!";
        const string Committed_Shareable = "Committed_Shareable!";
        const string Committed_Total = "Committed_Total";
        const string ManagedHeapSize = "Managed Heap(TotalSize)";
        const string ManagedHeapFree = "Managed Heap(Free)!";
        const string ManagedHeapAllocated = "Managed Heap(Allocated)!";
        const string AllocatedTotal = "Allocated(Total)";


        /// <summary>
        /// Construct a memory analyzer
        /// </summary>
        /// <param name="heap">First Heap (mandatory)</param>
        /// <param name="heap2">Second Heap which can be null. </param>
        /// <param name="bLiveOnly">If true only rooted objects are considered. This takes longer to execute but is more accurate. Otherwise temp objects which have not yet been collected also show up.</param>
        /// <param name="bGetVMMapData">If true then from running processes the VMMap information is read.</param>
        /// <param name="info">Target process pid and/or dump files names.</param>
        /// <param name="displayunit">Units in which bytes should be displayed</param>
        /// <param name="timeFormat">.NET Format string for time and date. If value is Invariant then the invariant culture is used to format.</param>
        /// <param name="context">Optional context column which is printed to each output line when CSV output is enabled to e.g. dump the memory for test run nr 1, 2, 3, which gives nice metric in Excel later.</param>
        public MemAnalyzer(ClrHeap heap, ClrHeap heap2, bool bLiveOnly, bool bGetVMMapData, TargetInformation info, DisplayUnit displayunit=DisplayUnit.MB, string timeFormat=null, string context = null) 
            :base(heap, heap2, bLiveOnly, displayunit)
        {
            GetVMMapData = bGetVMMapData;
            TargetInfo = info;
            ProcessName = TargetInfo.IsLiveProcess? TargetInformation.GetProcessName(TargetInfo.Pid1) : "";
            CmdLine = TargetInfo.IsLiveProcess ? TargetInformation.GetProcessCmdLine(TargetInfo.Pid1) : "";
            DateTime now = TargetInfo.CurrentTimeOrDumpCreationDate;
            TimeAndOrDate = timeFormat == "Invariant" ? now.ToString(CultureInfo.InvariantCulture) : now.ToString(timeFormat);
            Context = context;
        }

        /// <summary>
        /// Dump types by size or by count.
        /// </summary>
        /// <param name="topN">Only print the first N types ordered by size or count.</param>
        /// <param name="orderBySize">If true types are sorted by size. Otherwise by count.</param>
        /// <returns>Allocated memory in KB if VMMap data is available. Otherwise only the allocated managed heap size in KB is returned.</returns>
        /// <remarks>The returned value is returned, if no other error has occurred from the Main method. That allows test automation to trigger e.g. a dump of 
        /// a leak was detected or to automatically enable memory profiling if the allocated memory reached a threshold.</remarks>
        public int DumpTypes(int topN, bool orderBySize, int minCount)
        {
            var typeInfosTask = Task.Factory.StartNew( () => GetTypeStatistics(Heap, LiveOnly));
            int allocatedMemoryInKB = 0;
            if (Heap2 != null)
            {
                var typeInfos2 = GetTypeStatistics(Heap2, LiveOnly);

                VMMapData vmmap = null;
                VMMapData vmmap2 = null;
                
                if (this.GetVMMapData)
                {
                    vmmap2 = GetVMMapDataFromProcess(false, TargetInfo, Heap2);
                    typeInfosTask.Wait();

                    vmmap = GetVMMapDataFromProcess(true, TargetInfo, Heap);
                }

                // Get allocated diff
                allocatedMemoryInKB = PrintTypeStatisticsDiff(typeInfosTask.Result, typeInfos2, vmmap, vmmap2, topN, minCount, orderBySize);
            }
            else
            {
                // get allocated memory
                allocatedMemoryInKB = PrintTypeStatistics(topN, minCount, orderBySize, typeInfosTask);
            }

            return allocatedMemoryInKB;
        }

        /// <summary>
        /// Write one line of type statistics metric. This method adds extra columns for CSV output to see which process, cmd line and time 
        /// the data was generated to make it possible to record data in append mode. 
        /// </summary>
        /// <param name="allocated">Allocated bytes</param>
        /// <param name="instances">Allocates Intances</param>
        /// <param name="type">Type name</param>
        void WriteTypeStatisticsLine(long allocated, long instances, string type)
        {
            // Default format string for console output. 
            // fmt is ignored for CSV output where all columns are simply \t separated. 
            string fmt = "{0,-17:N0}\t{1,-17:N0}\t{2}";

            if (IsFirstLine)
            {
                string[] header = new string[] { "Allocated" + DisplayUnit, "Instances(Count)", "Type" };

                if (OutputStringWriter.CsvOutput)
                {
                    header = header.Concat(new string[] { "ProcessId", "Time", "CommandLine", "Age(s)", "Name", "Context" }).ToArray();
                }

                // write header
                OutputStringWriter.FormatAndWriteHeader(fmt,header);
                IsFirstLine = false;
            }

            if (OutputStringWriter.CsvOutput)
            {
                OutputStringWriter.FormatAndWrite(fmt, allocated, instances, type, TargetInfo.IsLiveProcess ? TargetInfo.Pid1.ToString() : "", TimeAndOrDate, CmdLine,  TargetInfo.ProcessAgeInSeconds, TargetInfo.ProcessName, Context);
            }
            else
            {
                OutputStringWriter.FormatAndWrite(fmt, allocated, instances == 0 ? "" : (object) instances, type);
            }
        }

        /// <summary>
        /// Print type statistics. If output is a CSV file time and process information are appended to allow subsequent writes into one big CSV file for long data series.
        /// </summary>
        /// <param name="topN"></param>
        /// <param name="orderBySize"></param>
        /// <param name="typeInfosTask"></param>
        /// <returns>Allocated memory in KB. If VMmap data is present the total allocated memory diff for allocated managed heap, heap, private, file mappings and sharable memory is returned.</returns>
        private int PrintTypeStatistics(int topN, int minCount, bool orderBySize, Task<List<TypeInfo>> typeInfosTask)
        {
            int allocatedMemoryInKB = 0;
            var typeInfos = typeInfosTask.Result;

            if( minCount > 0 )
            {
                typeInfos = typeInfos.Where(x => x.Count > minCount).ToList();
            }
            typeInfos.Sort((x, y) => orderBySize ? y.AllocatedSizeInBytes.CompareTo(x.AllocatedSizeInBytes) : y.Count.CompareTo(x.Count));

            // can be null if only live objects are considered.
            var free = typeInfos.FirstOrDefault(x => x.Name == FreeTypeName);

            if (topN > 0)
            {
                foreach (var type in typeInfos.Take(topN))
                {
                    if (free != null && type == free)
                    {
                        continue;
                    }
                    WriteTypeStatisticsLine(type.AllocatedSizeInBytes / (long)DisplayUnit, type.Count, type.Name);
                }
            }

            // Total heap size is only possible to calculate if the free objects are included 
            if (free != null)
            {
                WriteTypeStatisticsLine(free.AllocatedSizeInBytes / (long)DisplayUnit, free.Count, ManagedHeapFree);
            }

            float managedAllocatedBytes = typeInfos.Where(x => free != null ? x != free : true).Sum(x => (float)x.AllocatedSizeInBytes);

            WriteTypeStatisticsLine( (long) (managedAllocatedBytes / (long)DisplayUnit), typeInfos.Where(x => x != free).Sum(x => (long)x.Count), ManagedHeapAllocated);
            WriteTypeStatisticsLine(Heap.GetTotalHeapSize(), 0,  ManagedHeapSize);

            allocatedMemoryInKB = (int)(managedAllocatedBytes / (long)DisplayUnit.KB);
            if (GetVMMapData)
            {
                VMMapData data = GetVMMapDataFromProcess(true, TargetInfo, Heap);
                WriteVMMapData(GetSimpleTypeFormatter(DisplayUnit), data);
                WriteTypeStatisticsLine((long) ((managedAllocatedBytes + data.AllocatedBytesWithoutManagedHeap) / (long)DisplayUnit), 0, AllocatedTotal);
                allocatedMemoryInKB += (int)(data.AllocatedBytesWithoutManagedHeap / (long)DisplayUnit.KB);
            }

            return allocatedMemoryInKB;
        }

        /// <summary>
        /// Print type statistics diff.
        /// </summary>
        /// <param name="typeInfos"></param>
        /// <param name="typeInfos2"></param>
        /// <param name="vmmap"></param>
        /// <param name="vmmap2"></param>
        /// <param name="topN"></param>
        /// <param name="orderBySize"></param>
        /// <param name="minCount"></param>
        /// <returns>Allocated memory diff in KB. If VMmap data is present the total allocated memory diff for allocated managed heap, heap, private, file mappings and sharable memory is returned.</returns>
        private int PrintTypeStatisticsDiff(List<TypeInfo> typeInfos, List<TypeInfo> typeInfos2, VMMapData vmmap, VMMapData vmmap2, int topN, int minCount, bool orderBySize)
        {
            int allocatedMemoryInKB = 0;

            TypeDiffStatistics delta = GetDiffStatistics(typeInfos, typeInfos2, orderBySize);

            if (minCount > 0)
            {
                delta.TypeDiffs = delta.TypeDiffs.Where(x => Math.Abs( orderBySize ?  x.InstanceCountDiff : x.AllocatedBytesDiff) > minCount).ToList();
            }

            string fmt = "{0,-12:N0}\t{1,-17:N0}\t{2,-11:N0}\t{3,-11:N0}\t{4,-17:N0}\t{5,-18:N0}" +
                        "\t{6,-14}\t{7,-15}\t{8}";

            OutputStringWriter.FormatAndWriteHeader(fmt, $"Delta({DisplayUnit})", "Delta(Instances)", "Instances", "Instances2", $"Allocated({DisplayUnit})", $"Allocated2({DisplayUnit})", 
                        "AvgSize(Bytes)", "AvgSize2(Bytes)", "Type");

            long unitDivisor = (long)DisplayUnit;

            if (topN > 0)
            {
                foreach (var diff in delta.TypeDiffs.Take(topN))
                {
                    if (diff.Name == FreeTypeName)
                    {
                        continue;
                    }


                    OutputStringWriter.FormatAndWrite(fmt, diff.AllocatedBytesDiff/unitDivisor, diff.InstanceCountDiff, diff?.Info.SafeCount(), diff?.Info2.SafeCount(), diff?.Info.TotalSize()/unitDivisor,
                        diff?.Info2.TotalSize()/unitDivisor, diff?.Info?.AverageSizePerInstance, diff?.Info2?.AverageSizePerInstance, diff.Name);
                }
            }

            allocatedMemoryInKB = (int) ( delta.DeltaBytes / (long) DisplayUnit.KB );
            OutputStringWriter.FormatAndWrite(fmt, delta.DeltaBytes / unitDivisor, delta.DeltaInstances, delta.Count, delta.Count2, delta.SizeInBytes / unitDivisor,
                                      delta.SizeInBytes2 / unitDivisor, "", "", ManagedHeapAllocated);
            long heap1Size = Heap.GetTotalHeapSize();
            long heap2Size = Heap2.GetTotalHeapSize();

            OutputStringWriter.FormatAndWrite(fmt, (heap2Size - heap1Size) / unitDivisor, 0, 0, 0, heap1Size / unitDivisor, heap2Size / unitDivisor, "", "", ManagedHeapSize);

            if ( vmmap != null && vmmap2 != null && vmmap.HasValues && vmmap2.HasValues)
            {
                var diff = vmmap2 - vmmap;
                WriteVMMapDataDiff(GetSimpleDiffFormatter(fmt, DisplayUnit), vmmap, vmmap2, diff);
                OutputStringWriter.FormatAndWrite(fmt, (diff.AllocatedBytesWithoutManagedHeap+delta.DeltaBytes) / unitDivisor, "", "", "",  
                    ( delta.SizeInBytes + vmmap.AllocatedBytesWithoutManagedHeap ) / unitDivisor, ( vmmap2.AllocatedBytesWithoutManagedHeap + delta.SizeInBytes2 ) / unitDivisor, ""
                    , "", AllocatedTotal);

                // When VMMap data is present add the other memory types which usually leak as well also to the allocation number.
                allocatedMemoryInKB += (int) ( diff.AllocatedBytesWithoutManagedHeap  / (long)DisplayUnit.KB);
            }

            return allocatedMemoryInKB;
        }

        static List<TypeInfo> GetTypeStatistics(ClrHeap heap, bool bLiveOnly)
        {
            List<TypeInfo> typeInfos = null;
            if (heap.CanWalkHeap)
            {
                var stats = from o in GetObjectAddresses(heap, bLiveOnly)
                            let t = heap.GetObjectType(o)
                            where t != null
                            select new KeyValuePair<ClrType, ulong>(t, o);


                Dictionary<string, TypeInfo> infos = new Dictionary<string, TypeInfo>();
                foreach (var stat in stats)
                {
                    TypeInfo info = null;
                    if (!infos.TryGetValue(stat.Key.Name, out info))
                    {
                        info = new TypeInfo { Name = stat.Key.Name };
                        infos[stat.Key.Name] = info;
                    }

                    info.Count++;
                    info.AllocatedSizeInBytes += (long)stat.Key.GetSize(stat.Value);
                }

                typeInfos = infos.Values.ToList();
            }
            else
            {
                Console.WriteLine("Error: Managed heap is not in a valid state!");
            }

            return typeInfos;
        }


        TypeDiffStatistics GetDiffStatistics(List<TypeInfo> typeInfos, List<TypeInfo> typeInfos2, bool orderBySize)
        {
            HashSet<string> commonTypes = new HashSet<string>(typeInfos.Select(x => x.Name).Concat(typeInfos2.Select(x => x.Name)));
            var typeInfosDict = typeInfos.ToDictionary(x => x.Name);
            var typeInfosDict2 = typeInfos2.ToDictionary(x => x.Name);

            List<TypeInfoDiff> diffs = new List<TypeInfoDiff>();
            foreach (var type in commonTypes)
            {
                TypeInfo info = null;
                TypeInfo info2 = null;

                typeInfosDict.TryGetValue(type, out info);
                typeInfosDict2.TryGetValue(type, out info2);

                diffs.Add(new TypeInfoDiff
                {
                    AllocatedBytesDiff = info2.TotalSize() - info.TotalSize(),
                    InstanceCountDiff = info2.SafeCount() - info.SafeCount(),
                    Info = info,
                    Info2 = info2,
                });
            }

            // delta bytes and instances only show the allocated objects
            // on the heap and not sum free and allocated objects together
            diffs = diffs.Where(x => x.Name != FreeTypeName)
                         .OrderByDescending(x => orderBySize ? Math.Abs(x.AllocatedBytesDiff) : Math.Abs(x.InstanceCountDiff)).ToList();

            var deltaBytes = diffs.Sum(x => x.AllocatedBytesDiff);
            var deltaInstances = diffs.Sum(x => x.InstanceCountDiff);
            var count = (long)diffs.Sum(x => x?.Info.SafeCount());
            var count2 = (long)diffs.Sum(x => x?.Info2.SafeCount());
            var size = (long)diffs.Sum(x => x?.Info.TotalSize());
            var size2 = (long)diffs.Sum(x => x?.Info2.TotalSize());

            TypeDiffStatistics lret = new TypeDiffStatistics
            {
                TypeDiffs = diffs,
                Count = count,
                Count2 = count2,
                DeltaBytes = deltaBytes,
                DeltaInstances = deltaInstances,
                SizeInBytes = size,
                SizeInBytes2 = size2
            };

            return lret;
        }

        /// <summary>
        /// Write VMMap data with given formatter function.
        /// </summary>
        /// <param name="formatter"></param>
        /// <param name="vm"></param>
        void WriteVMMapData(Action<string, long> formatter, VMMapData vm)
        {
            // free blocks only play a role in x86 
            if (vm.LargestFreeBlockBytes < 4 * 1024 * 1024 * 1024L)
            {
                formatter(Reserved_LargestFreeBlock, vm.LargestFreeBlockBytes);
            }
            formatter(Reserved_Stack, vm.Reserved_Stack);
            formatter(Committed_Dll, vm.Committed_DllBytes);
            formatter(Committed_Heap, vm.Committed_HeapBytes);
            formatter(Committed_MappedFile, vm.Committed_MappedFileBytes);
            formatter(Committed_Private, vm.Committed_PrivateBytes);
            formatter(Committed_Shareable, vm.Committed_ShareableBytes);
            formatter(Committed_Total, vm.TotalCommittedBytes);
        }

        /// <summary>
        /// Write VMMap data with given formatter function.
        /// </summary>
        /// <param name="formatter"></param>
        /// <param name="vm"></param>
        void WriteVMMapDataDiff(Action<string, long, long, long> formatter, VMMapData vm, VMMapData vm2, VMMapData diff)
        {
            formatter(Reserved_Stack, diff.Reserved_Stack, vm.Reserved_Stack, vm2.Reserved_Stack);
            // free blocks only play a role in x86 
            if (vm.LargestFreeBlockBytes < 4 * 1024 * 1024 * 1024L)
            {
                formatter(Reserved_LargestFreeBlock, diff.LargestFreeBlockBytes, vm.LargestFreeBlockBytes, vm2.LargestFreeBlockBytes);
            }

            formatter(Committed_Dll, diff.Committed_DllBytes, vm.Committed_DllBytes, vm2.Committed_DllBytes);
            formatter(Committed_Heap, diff.Committed_HeapBytes, vm.Committed_HeapBytes, vm2.Committed_HeapBytes);
            formatter(Committed_MappedFile, diff.Committed_MappedFileBytes, vm.Committed_MappedFileBytes, vm2.Committed_MappedFileBytes);
            formatter(Committed_Private, diff.Committed_PrivateBytes, vm.Committed_PrivateBytes, vm2.Committed_PrivateBytes);
            formatter(Committed_Shareable, diff.Committed_ShareableBytes, vm.Committed_ShareableBytes, vm2.Committed_ShareableBytes);
            formatter(Committed_Total, diff.TotalCommittedBytes, vm.TotalCommittedBytes, vm2.TotalCommittedBytes);
        }

        /// <summary>
        /// Format a type diff format string where only diff, size, size2 and type name are printed to the output.
        /// </summary>
        /// <param name="fmt">Type diff format string.</param>
        /// <param name="unit">Unit in which bytes are printed.</param>
        /// <returns>Delegate which accepts sizeDiff, size1, size2 as long values.</returns>
        Action<string, long, long, long> GetSimpleDiffFormatter(string fmt, DisplayUnit unit)
        {
            return (typeName, sizeInBytesDiff, sizeInBytes1, sizeInBytes2) =>
            {
                long unitDivisor = (long)unit;
                OutputStringWriter.FormatAndWrite(fmt, (long) (sizeInBytesDiff/unitDivisor), "", "", "", (long) (sizeInBytes1/ unitDivisor) , (long) (sizeInBytes2/unitDivisor), "", "", typeName+unit);
            };
        }

        /// <summary>
        /// Format a type with no instance count.
        /// </summary>
        /// <param name="fmt">Type format string.</param>
        /// <param name="unit">Unit in which tye type is formatted.</param>
        /// <returns>Delegate which will format the type and its size in the respective unit.</returns>
        Action<string, long> GetSimpleTypeFormatter(DisplayUnit unit)
        {
            return (typeName, sizeInBytes) =>
            {
                long unitDivisor = (long)unit;
                WriteTypeStatisticsLine((long)(sizeInBytes / unitDivisor), 0, typeName);
            };
        }


        private static VMMapData GetVMMapDataFromProcess(bool bFirstProcess, TargetInformation targetInfo, ClrHeap heap)
        {
            int pid = bFirstProcess ? targetInfo.Pid1 : targetInfo.Pid2;
            VMMapData data = new VMMapData();

            if (pid != 0)
            {
                // we must first detach CLRMD or VMMAp will block at least in x64 in the target process to get heap information.
                // Play safe and do not try this asynchronously.
                heap.Runtime.DataTarget.Dispose();
                data = StartVMMap(pid, null);
            }
            else
            {
                string existingVMMapFile = bFirstProcess ? targetInfo.DumpVMMapFile1 : targetInfo.DumpVMMapFile2;
                if (existingVMMapFile != null)
                {
                    data = StartVMMap(0, existingVMMapFile);
                }
            }

            return data;
        }


        static VMMapData StartVMMap(int pid, string existingVMmapFile)
        {
            VMMap map = pid != 0 ? new VMMap(pid) : new VMMap(existingVMmapFile);

            var lret = map.GetMappingData();
            if (pid != 0)
            {
                lret.Pid = pid;
            }
            return lret;
        }
    }

}
