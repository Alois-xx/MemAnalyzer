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

}
