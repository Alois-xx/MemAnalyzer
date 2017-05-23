using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemAnalyzer
{
    /// <summary>
    /// Encapsulates PID/DumpFile of the currently analyzed process/es
    /// </summary>
    class TargetInformation
    {
        public int Pid1 { get; set; }
        public string DumpFileName1 { get; set;  }
        public int Pid2 { get; set; }
        public string DumpFileName2 { get; set; }

        public const string VMMapPostFix = "_VMMap.csv";

        /// <summary>
        /// Get from dump file 1 the associated VMMap file which is of the format file.dmp -> file_VMMap.csv
        /// If the file does not exist null is returned.
        /// </summary>
        public string DumpVMMapFile1
        {
            get
            {
                return GetExistingVMMapFile(DumpFileName1);
            }
        }

        /// <summary>
        /// Return true if we have two processes to watch. One can be a memory dump or a live process.
        /// </summary>
        public bool IsProcessCompare
        {
            get
            {
                return (Pid1 != 0 || DumpFileName1 != null) &&
                       (Pid2 != 0 || DumpFileName2 != null);
            }
        }

        /// <summary>
        /// Get from dump file 2 the associated VMMap file which is of the format file.dmp -> file_VMMap.csv.
        /// If the file does not exist null is returned.
        /// </summary>
        public string DumpVMMapFile2
        {
            get
            {
                return GetExistingVMMapFile(DumpFileName2);
            }
        }

        /// <summary>
        /// Get from a given dump file name the associated vmmap file name.
        /// </summary>
        /// <param name="dumpFileName">Dump file name</param>
        /// <returns></returns>
        public static string GetAssociatedVMMapFile(string dumpFileName)
        {
            if( String.IsNullOrEmpty(dumpFileName) )
            {
                return null;
            }

            string file = Path.GetFileNameWithoutExtension(dumpFileName);
            string dir = Path.GetDirectoryName(dumpFileName);
            string vmmapFile =  Path.Combine(dir, file + VMMapPostFix);
            return vmmapFile;
        }

        public static string GetExistingVMMapFile(string dumpFileName)
        {
            string vmmapFile = GetAssociatedVMMapFile(dumpFileName);
            if (!File.Exists(vmmapFile))
            {
                vmmapFile = null;
            }

            return vmmapFile;
        }
    }
}
