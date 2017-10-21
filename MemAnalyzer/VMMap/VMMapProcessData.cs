using System;

namespace MemAnalyzer
{
    class VMMapProcessData : VMMapData
    {
        ProcessContextInfo _ProcessContext;

        public ProcessContextInfo ProcessContext { get => _ProcessContext; set => _ProcessContext = value; }

        public VMMapProcessData()
        { }

        public VMMapProcessData(ProcessContextInfo infos)
        {
            _ProcessContext = infos;
        }

        internal void Add(string colName, long allocatesBytes)
        {
            switch(colName)
            {
                case Col_Committed_Dll:
                    Committed_DllBytes = allocatesBytes;
                    break;
                case Col_Committed_Heap:
                    Committed_HeapBytes = allocatesBytes;
                    break;
                case Col_Committed_MappedFile:
                    Committed_MappedFileBytes = allocatesBytes;
                    break;
                case Col_Committed_Private:
                    Committed_PrivateBytes = allocatesBytes;
                    break;
                case Col_Committed_Shareable:
                    Committed_ShareableBytes = allocatesBytes;
                    break;
                case Col_Committed_Total:
                    // this is a calculated field
                    break;
                case Col_Reserved_LargestFreeBlock:
                    LargestFreeBlockBytes = allocatesBytes;
                    break;
                case Col_Reserved_Stack:
                    Reserved_Stack = allocatesBytes;
                    break;
                default:
                    throw new NotSupportedException($"Column {colName} is currently not mapped to a field.");
            }
        }
    }
}
