using System.Collections.Generic;

namespace MemAnalyzer
{
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

}
