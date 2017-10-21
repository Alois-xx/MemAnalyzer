using Microsoft.Diagnostics.Runtime;

namespace MemAnalyzer
{
    /// <summary>
    /// Wrapper around object address and a given ClrType.
    /// </summary>
    struct TypeInstance
    {
        public ulong Address;
        public ClrType Type;

        public TypeInstance(ulong address, ClrType type)
        {
            Address = address;
            Type = type;
        }
    }
}
