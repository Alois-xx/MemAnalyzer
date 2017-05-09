using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemAnalyzer
{
    /// <summary>
    /// Print debug messages if flag is enabled.
    /// </summary>
    static class DebugPrinter
    {
        /// <summary>
        /// Primitive wrapper to spit out some debug messages.
        /// </summary>
        /// <param name="fmt"></param>
        /// <param name="args"></param>
        public static void Write(string fmt, params object[] args)
        {
            if( Program.IsDebug )
            {
                Console.WriteLine(fmt, args);
            }
        }
    }
}
