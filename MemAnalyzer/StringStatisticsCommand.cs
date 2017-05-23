using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MemAnalyzer
{
    /// <summary>
    /// Print which strings need most of the memory and their was. 
    /// Additionally it can also compare two memory dumps or one live process and one dump to e.g. check if an optimization did actually help.
    /// </summary>
    class StringStatisticsCommand : MemAnalyzerBase
    {

        /// <summary>
        /// Init String Statistics
        /// </summary>
        /// <param name="heap">First Heap</param>
        /// <param name="heap2">Second Heap</param>
        /// <param name="bLiveOnly">If true only consider reachable objects. Otherwise temporary not yet allocated objects are also counted. This is faster but less accurate.</param>
        public StringStatisticsCommand(ClrHeap heap, ClrHeap heap2, bool bLiveOnly, DisplayUnit displayUnit):base(heap, heap2, bLiveOnly, displayUnit)
        {
        }

        static StringAnalysisResult Analyze(ClrHeap heap, bool bLiveOnly)
        {
            var stringUsages = new Dictionary<string, ObjectStatistics>();

            foreach (var instance in GetObjectAddresses(heap, bLiveOnly))
            {
                var type = heap.GetObjectType(instance);
                if (type != null && type.IsString)
                {
                    var value = (string)type.GetValue(instance);
                    ObjectStatistics countAndSize = null;
                    if (stringUsages.TryGetValue(value, out countAndSize))
                    {
                        countAndSize.InstanceCount++;
                    }
                    else
                    {
                        var size = type.GetSize(instance);
                        stringUsages[value] = new ObjectStatistics { SizePerInstance = (long) size, InstanceCount = 1, SampleAddress = instance };
                    }
                }
            }

            var stringWasteInBytes = stringUsages.Values.Sum(o => (long)o.SizePerInstance * (long)(o.InstanceCount - 1));
            var stringObjectCount = stringUsages.Values.Sum(o => (long)o.InstanceCount);
            var stringsAllocatedBytes = stringUsages.Values.Sum(o => (long)o.AllocatedInBytes);

            return new StringAnalysisResult
            {
                StringCounts = stringUsages,
                StringWasteInBytes = stringWasteInBytes,
                StringObjectCount = stringObjectCount,
                StringsAllocatedInBytes = stringsAllocatedBytes
            };
        }

        /// <summary>
        /// Print only first 50 chars of a string to prevent cluttering the output too much.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        string GetShortString(string str)
        {
            if( str.Length > 50 )
            {
                str = str.Substring(0, 50) + "...";
            }

            return str.Replace(Environment.NewLine, "");
        }
        public void Execute(int topN, bool bShowAddress)
        {
            Task<StringAnalysisResult> res = Task.Factory.StartNew<StringAnalysisResult>(() => Analyze(Heap, LiveOnly));
            if (Heap2 != null)
            {
                StringAnalysisResult res2 = Analyze(Heap2, LiveOnly);
                PrintDiff(topN, res.Result, res2);
            }
            else
            {
                if (topN > 0)
                {
                    var sorted = res.Result.StringCounts.OrderByDescending(kvp => kvp.Value.InstanceCount).Take(topN);
                    OutputStringWriter.FormatAndWrite("{0}\t{1}\t{2}", "Strings(Count)", $"Waste({DisplayUnit})", "String");
                    string fmt = "{0,-12}\t{1,-11:N0}\t{2}";
                    foreach (var kvp in sorted)
                    {
                        string addressString = bShowAddress ? " 0x"+ kvp.Value.SampleAddress.ToString("X") : "";
                        OutputStringWriter.FormatAndWrite(fmt, kvp.Value.InstanceCount, ((kvp.Value.InstanceCount - 1L) * kvp.Value.SizePerInstance) / (long)DisplayUnit, GetShortString(kvp.Key) + addressString);
                    }
                }

                if (!OutputStringWriter.CsvOutput)
                {
                    Console.WriteLine();
                    Console.WriteLine("Summary");
                    Console.WriteLine("==========================================");
                    Console.WriteLine($"Strings                 {res.Result.StringObjectCount,12:N0} count");
                    Console.WriteLine($"Allocated Size          {res.Result.StringsAllocatedInBytes/(long)DisplayUnit,12:N0} {DisplayUnit}");
                    Console.WriteLine($"Waste Duplicate Strings {res.Result.StringWasteInBytes/(long)DisplayUnit,12:N0} {DisplayUnit}");
                }
            }
        }


        private void PrintDiff(int topN, StringAnalysisResult res, StringAnalysisResult res2)
        {
            var top = res.StringCounts.OrderByDescending(kvp => kvp.Value.InstanceCount).Take(topN).ToDictionary(x => x.Key, x => x.Value);
            var top2 = res2.StringCounts.OrderByDescending(kvp => kvp.Value.InstanceCount).Take(topN).ToDictionary(x=> x.Key, x=> x.Value);

            HashSet<string> uniqueStringValues = new HashSet<string>(top.Select(x => x.Key).Concat(top2.Select(x => x.Key)).ToArray());
            List<StringDiff> diffs = new List<StringDiff>();
            foreach (var stringValue in uniqueStringValues)
            {
                ObjectStatistics stat = null;
                ObjectStatistics stat2 = null;
                top.TryGetValue(stringValue, out stat);
                top2.TryGetValue(stringValue, out stat2);
                diffs.Add(new StringDiff
                {
                    DiffInBytes = (stat2 != null ? stat2.AllocatedInBytes : 0) - (stat != null ? stat.AllocatedInBytes : 0),
                    InstanceDiffCount = (stat2 != null ? stat2.InstanceCount : 0) - (stat != null ? stat.InstanceCount : 0),
                    Stat = stat,
                    Stat2 = stat2,
                    Value = stringValue
                });
            }

            var sortedDiffs = diffs.OrderByDescending(x => Math.Abs(x.DiffInBytes)).ToArray();
            Console.WriteLine("String Allocation Diff Statistics");
            string fmtString = "{0,-12:N0}\t{1,17:N0}\t{2,-11:N0}\t{3,-11:N0}\t{4,-17:N0}\t{5,-18:N0}\t{6}";
            OutputStringWriter.FormatAndWrite(fmtString, $"Delta({DisplayUnit})", "Delta(Instances)", "Instances", "Instances2", $"Allocated({DisplayUnit})", $"Allocated2({DisplayUnit})", "Value");

            long displayUnitDiv = (long) DisplayUnit;
            foreach(var diff in sortedDiffs)
            {
                OutputStringWriter.FormatAndWrite(fmtString, diff.DiffInBytes/ displayUnitDiv, diff.InstanceDiffCount, diff?.Stat?.InstanceCount, diff?.Stat2?.InstanceCount, diff?.Stat?.AllocatedInBytes/displayUnitDiv, diff?.Stat2?.AllocatedInBytes/displayUnitDiv, GetShortString(diff.Value));
            }

            var deltaCount = res2.StringObjectCount - res.StringObjectCount;
            var deltaWaste = res2.StringWasteInBytes - res.StringWasteInBytes;
            var deltaBytes = res2.StringsAllocatedInBytes - res.StringsAllocatedInBytes;

            OutputStringWriter.FormatAndWrite(fmtString, deltaBytes/displayUnitDiv, deltaCount, res.StringObjectCount, res2.StringObjectCount, 
                res.StringsAllocatedInBytes/ displayUnitDiv, res2.StringsAllocatedInBytes/ displayUnitDiv, "Strings(Total)");
        }

        class StringAnalysisResult
        {
            public Dictionary<string, ObjectStatistics> StringCounts;
            public long StringWasteInBytes;
            public long StringObjectCount;

            public long StringsAllocatedInBytes { get; internal set; }
        }
    }
}
