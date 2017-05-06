using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
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
        const string FreeTypeName = "Free";

        /// <summary>
        /// Construct a memory analyzer
        /// </summary>
        /// <param name="heap">First Heap (mandatory)</param>
        /// <param name="heap2">Second Heap which can be null. </param>
        /// <param name="bLiveOnly">If true only rooted objects are considered. This takes longer to execute but is more accurate. Otherwise temp objects which have not yet been collected also show up.</param>
        public MemAnalyzer(ClrHeap heap, ClrHeap heap2, bool bLiveOnly):base(heap, heap2, bLiveOnly)
        {
        }

        public void DumpTypes(int topN, bool orderBySize)
        {
            var typeInfosTask = Task.Factory.StartNew( () => GetTypeStatistics(Heap, LiveOnly));
            if (Heap2 != null)
            {
                var typeInfos2 = GetTypeStatistics(Heap2, LiveOnly);
                PrintTypeStatisticsDiff(typeInfosTask.Result, typeInfos2, topN, orderBySize);
            }
            else
            {
                var typeInfos = typeInfosTask.Result;
                typeInfos.Sort((x, y) => orderBySize ? y.AllocatedSizeInBytes.CompareTo(x.AllocatedSizeInBytes) : y.Count.CompareTo(x.Count));
                string fmt = "{0,-17:N0}\t{1,-17:N0}\t{2}";
                OutputStringWriter.FormatAndWrite(fmt, "Allocated(Bytes)", "Instances(Count)", "Type");

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
                        OutputStringWriter.FormatAndWrite(fmt, type.AllocatedSizeInBytes, type.Count, type.Name);
                    }
                }

                // Total heap size is only possible to calculate if the free objects are included 
                if (free != null)
                {
                    OutputStringWriter.FormatAndWrite(fmt, typeInfos.Sum(x => (float)x.AllocatedSizeInBytes), "", "Total(Heap Size)");
                    OutputStringWriter.FormatAndWrite(fmt, free.AllocatedSizeInBytes, "", "Total(Free)");
                }

                OutputStringWriter.FormatAndWrite(fmt, typeInfos.Where(x => free != null  ? x != free : true).Sum(x => (float)x.AllocatedSizeInBytes), typeInfos.Where(x => x != free).Sum(x => (float)x.Count), "Total(Allocated)");
            }
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
            var count = (long) diffs.Sum(x => x?.Info.SafeCount());
            var count2 = (long) diffs.Sum(x => x?.Info2.SafeCount());
            var size = (long) diffs.Sum(x => x?.Info.TotalSize());
            var size2 = (long) diffs.Sum(x => x?.Info2.TotalSize());

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

        private void PrintTypeStatisticsDiff(List<TypeInfo> typeInfos, List<TypeInfo> typeInfos2, int topN, bool orderBySize)
        {
            TypeDiffStatistics delta = GetDiffStatistics(typeInfos, typeInfos2, orderBySize);

            string fmt = "{0,-12:N0}\t{1,-17:N0}\t{2,-11:N0}\t{3,-11:N0}\t{4,-17:N0}\t{5,-18:N0}" +
                        "\t{6,-14}\t{7,-15}\t{8}";

            OutputStringWriter.FormatAndWrite(fmt, "Delta(Bytes)", "Delta(Instances", "Instances", "Instances2", "Allocated(Bytes)", "Allocated2(Bytes)", 
                        "AvgSize(Bytes)", "AvgSize2(Bytes)", "Type");
            if (topN > 0)
            {
                foreach (var diff in delta.TypeDiffs.Take(topN))
                {
                    if (diff.Name == FreeTypeName)
                    {
                        continue;
                    }

                    OutputStringWriter.FormatAndWrite(fmt, diff.AllocatedBytesDiff, diff.InstanceCountDiff, diff?.Info.SafeCount(), diff?.Info2.SafeCount(), diff?.Info.TotalSize(),
                        diff?.Info2.TotalSize(), diff?.Info?.AverageSizePerInstance, diff?.Info2?.AverageSizePerInstance, diff.Name);
                }
            }

            const string NA = "N.A.";
            OutputStringWriter.FormatAndWrite(fmt, delta.DeltaBytes, delta.DeltaInstances, delta.Count, delta.Count2, delta.SizeInBytes, delta.SizeInBytes2, NA, NA, "Total");

        }
    }

}
