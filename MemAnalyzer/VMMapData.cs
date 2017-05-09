using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemAnalyzer
{
    class VMMapData
    {
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
    }
}
