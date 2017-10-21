namespace MemAnalyzer
{
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

}
