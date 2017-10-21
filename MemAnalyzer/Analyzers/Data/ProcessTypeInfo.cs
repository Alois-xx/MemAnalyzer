using System;

namespace MemAnalyzer
{
    /// <summary>
    /// From a CSV file we get not only the type information but additional fields
    /// </summary>
    internal class ProcessTypeInfo : TypeInfo
    {
        ProcessContextInfo _ProcessContext;

        public ProcessContextInfo ProcessContext { get => _ProcessContext; set => _ProcessContext = value; }

        public ProcessTypeInfo(ProcessContextInfo infos)
        {
            _ProcessContext = infos;
        }
    }

}
