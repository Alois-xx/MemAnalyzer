using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemAnalyzer
{
    /// <summary>
    /// Extensions used by string formatting routines which need to deal with null references because some TypeInfo instances can be null in the analysis results
    /// if for this type in another dump the type was not present at all.
    /// </summary>
    static class InternalExtensions
    {
        public static long SafeCount(this TypeInfo info)
        {
            if (info == null)
                return 0;
            return info.Count;
        }

        public static long TotalSize(this TypeInfo info)
        {
            if (info == null)
                return 0;
            return info.AllocatedSizeInBytes;
        }

        public static long GetTotalHeapSize(this ClrHeap heap)
        {
            return heap.Segments.Sum(seg => (long)seg.Length);
        }
    }
}