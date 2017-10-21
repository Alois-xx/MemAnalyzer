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
        /// <summary>
        /// Row names in output. Rows with a ! in their name are not aggregated values
        /// which makes it easy to filter for them in Pivot Charts with ! as text filter
        /// </summary>
        internal const string Col_Reserved_LargestFreeBlock = "Reserved_LargestFreeBlock";
        internal const string Col_Reserved_Stack = "Reserved_Stack";
        internal const string Col_Committed_Dll = "Committed_Dll";
        internal const string Col_Committed_Heap = "Committed_Heap!";
        internal const string Col_Committed_MappedFile = "Committed_MappedFile!";
        internal const string Col_Committed_Private = "Committed_Private!";
        internal const string Col_Committed_Shareable = "Committed_Shareable!";
        internal const string Col_Committed_Total = "Committed_Total";


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
            var lret = new VMMapData
            {
                Committed_DllBytes = x.Committed_DllBytes - y.Committed_DllBytes,
                Committed_HeapBytes = x.Committed_HeapBytes - y.Committed_HeapBytes,
                Committed_ManagedHeapBytes = x.Committed_ManagedHeapBytes - y.Committed_ManagedHeapBytes,
                Committed_MappedFileBytes = x.Committed_MappedFileBytes - y.Committed_MappedFileBytes,
                Committed_PageTable = x.Committed_PageTable - y.Committed_PageTable,
                Committed_PrivateBytes = x.Committed_PrivateBytes - y.Committed_PrivateBytes,
                Committed_ShareableBytes = x.Committed_ShareableBytes - y.Committed_ShareableBytes,
                Committed_Stack = x.Committed_Stack - y.Committed_Stack,

                LargestFreeBlockBytes = x.LargestFreeBlockBytes - y.LargestFreeBlockBytes,
                Reserved_DllBytes = x.Reserved_DllBytes - y.Reserved_DllBytes,
                Reserved_HeapBytes = x.Reserved_HeapBytes - y.Reserved_HeapBytes,
                Reserved_ManagedHeapBytes = x.Reserved_ManagedHeapBytes - y.Reserved_ManagedHeapBytes,
                Reserved_MappedFileBytes = x.Reserved_MappedFileBytes - y.Reserved_MappedFileBytes,
                Reserved_PageTable = x.Reserved_PageTable - y.Reserved_PageTable,
                Reserved_PrivateBytes = x.Reserved_PrivateBytes - y.Reserved_PrivateBytes,
                Reserved_ShareableBytes = x.Reserved_ShareableBytes - y.Reserved_ShareableBytes,
                Reserved_Stack = x.Reserved_Stack - y.Reserved_Stack
            };

            return lret;
        }

        /// <summary>
        /// If during deserialization from CSV file no data was present this is the way to check it.
        /// </summary>
        public bool IsEmpty
        {
            get => Committed_DllBytes == 0;
        }
    }
}
