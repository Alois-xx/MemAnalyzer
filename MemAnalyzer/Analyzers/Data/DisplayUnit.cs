namespace MemAnalyzer
{
    /// <summary>
    /// MemAnalyzer counts can be configured to show the values as rounded down values of each size. GB is not really useful since we do not show fractions
    /// which might confuse users with different locales where . , mean different things. 
    /// </summary>
    enum DisplayUnit
    {
        Bytes = 1,
        KB = 1024,
        MB = 1024*1024,
        GB = 1024*1024*1024,
    }

}
