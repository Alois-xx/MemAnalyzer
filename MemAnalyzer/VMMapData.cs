using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemAnalyzer
{
    /// <summary>
    /// This contains the parsed data from the VMMap SysInternals tool. 
    /// </summary>
    class VMMapData
    {
        public int Pid;

        public long Reserved_DllBytes;
        public long Reserved_MappedFileBytes;
        public long Reserved_ShareableBytes;
        public long Reserved_HeapBytes;
        public long Reserved_ManagedHeapBytes;
        public long Reserved_Stack;
        public long Reserved_PrivateBytes;
        public long Reserved_PageTable;

        public long Committed_DllBytes;
        public long Committed_MappedFileBytes;
        public long Committed_ShareableBytes;
        public long Committed_HeapBytes;
        public long Committed_ManagedHeapBytes;
        public long Committed_Stack;
        public long Committed_PrivateBytes;
        public long Committed_PageTable;

        public long LargestFreeBlockBytes;

        /// <summary>
        /// Returns sum of Committed Dll+MappedFiles+Shareable+Heap+ManagedHeap+Stack+PrivateBytes+PageTable
        /// </summary>
        public long TotalCommittedBytes
        {
            get
            {
                return Committed_DllBytes + Committed_MappedFileBytes + Committed_ShareableBytes + Committed_HeapBytes + Committed_ManagedHeapBytes + Committed_Stack + Committed_PrivateBytes + Committed_PageTable;
            }
        }

        /// <summary>
        /// Returns sum of mapped files + shareable + heap + private bytes which are frequently the sources of memory leak. 
        /// Exclude managed heap because we calculate the total allocated memory with the actually used managed heap to get exact allocation numbers.
        /// </summary>
        public long AllocatedBytesWithoutManagedHeap
        {
            get
            {
                return Committed_MappedFileBytes + Committed_ShareableBytes + Committed_HeapBytes + Committed_PrivateBytes;

            }
        }

        /// <summary>
        /// Return true if instance VMMap data. Otherwise false is returned.
        /// </summary>
        public bool HasValues
        {
            get { return Reserved_DllBytes != 0; }
        }

        /// <summary>
        /// For diffs subtraction is useful.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static VMMapData operator -(VMMapData x, VMMapData y)
        {
            var lret = new VMMapData();

            lret.Committed_DllBytes = x.Committed_DllBytes - y.Committed_DllBytes;
            lret.Committed_HeapBytes = x.Committed_HeapBytes - y.Committed_HeapBytes;
            lret.Committed_ManagedHeapBytes = x.Committed_ManagedHeapBytes - y.Committed_ManagedHeapBytes;
            lret.Committed_MappedFileBytes = x.Committed_MappedFileBytes - y.Committed_MappedFileBytes;
            lret.Committed_PageTable = x.Committed_PageTable - y.Committed_PageTable;
            lret.Committed_PrivateBytes = x.Committed_PrivateBytes - y.Committed_PrivateBytes;
            lret.Committed_ShareableBytes = x.Committed_ShareableBytes - y.Committed_ShareableBytes;
            lret.Committed_Stack = x.Committed_Stack - y.Committed_Stack;

            lret.LargestFreeBlockBytes = x.LargestFreeBlockBytes - y.LargestFreeBlockBytes;
            lret.Reserved_DllBytes = x.Reserved_DllBytes - y.Reserved_DllBytes;
            lret.Reserved_HeapBytes = x.Reserved_HeapBytes - y.Reserved_HeapBytes;
            lret.Reserved_ManagedHeapBytes = x.Reserved_ManagedHeapBytes - y.Reserved_ManagedHeapBytes;
            lret.Reserved_MappedFileBytes = x.Reserved_MappedFileBytes - y.Reserved_MappedFileBytes;
            lret.Reserved_PageTable = x.Reserved_PageTable - y.Reserved_PageTable;
            lret.Reserved_PrivateBytes = x.Reserved_PrivateBytes - y.Reserved_PrivateBytes;
            lret.Reserved_ShareableBytes = x.Reserved_ShareableBytes - y.Reserved_ShareableBytes;
            lret.Reserved_Stack = x.Reserved_Stack - y.Reserved_Stack;

            return lret;
        }
    }
}
