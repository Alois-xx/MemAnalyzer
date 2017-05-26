using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemAnalyzer
{
    /// <summary>
    /// Memory allocation information for a given type
    /// </summary>
    internal class TypeInfo
    {
        public string Name;
        public long AllocatedSizeInBytes;
        public long Count;
        public int AverageSizePerInstance
        {
            get { return (int)(AllocatedSizeInBytes / Count); }
        }
    }

    /// <summary>
    /// Memory allocation difference for a given type.
    /// </summary>
    class TypeInfoDiff
    {
        public string Name
        {
            get { return Info?.Name ?? Info2.Name; }
        }

        public long InstanceCountDiff = 0;
        public long AllocatedBytesDiff = 0;

        public TypeInfo Info;
        public TypeInfo Info2;
    }

    /// <summary>
    /// Contains the complete memory diff by types.
    /// </summary>
    class TypeDiffStatistics
    {
        /// <summary>
        /// List of differences by type sorted descending by Abs(delta(bytes)) or Abs(delta(instances))
        /// </summary>
        public List<TypeInfoDiff> TypeDiffs { get; set; }

        /// <summary>
        /// Total number of instances 2-1
        /// </summary>
        public long DeltaInstances { get; set; }

        /// <summary>
        /// Total number of allocated memory 2-1
        /// </summary>
        public long DeltaBytes { get; set; }

        /// <summary>
        /// Total allocated size in bytes
        /// </summary>
        public long SizeInBytes { get; set; }

        /// <summary>
        /// Total allocated size in bytes of dump/process 2
        /// </summary>
        public long SizeInBytes2 { get; set; }

        /// <summary>
        /// Total number of objects
        /// </summary>
        public long Count { get; set; }

        /// <summary>
        /// Total number of objects of dump/process 2
        /// </summary>
        public long Count2 { get; set; }
    }


    /// <summary>
    /// Contains diff Stat2-Stat
    /// </summary>
    class StringDiff
    {
        public long InstanceDiffCount;
        public long DiffInBytes;
        public ObjectStatistics Stat;
        public ObjectStatistics Stat2;
        public string Value;
    }

    /// <summary>
    /// Used for getting string statistics.
    /// </summary>
    class ObjectStatistics
    {
        public long InstanceCount;
        public long SizePerInstance;
        public ulong SampleAddress;

        public long AllocatedInBytes
        {
            get { return (long)(SizePerInstance * InstanceCount); }
        }
    }

}
