namespace MemAnalyzer
{
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
