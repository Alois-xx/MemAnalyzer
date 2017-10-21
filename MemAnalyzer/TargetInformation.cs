using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace MemAnalyzer
{
    /// <summary>
    /// Encapsulates PID/DumpFile of the currently analyzed process/es
    /// </summary>
    class TargetInformation
    {
        /// <summary>
        /// Optional rename for processes with given command line. Used for CSV output to get time series with descriptive process names.
        /// </summary>
        public ProcessRenamer Renamer { get; set; }

        public int Pid1 { get; set; }
        public string DumpFileName1 { get; set; }
        public int Pid2 { get; set; }
        public string DumpFileName2 { get; set; }

        public const string CSVExtension = ".csv";

        bool IsCsvFile(string fileName)
        {
            return Path.GetExtension(fileName).ToLower() == CSVExtension;
        }


        /// <summary>
        /// For CSV output we use the current time. But if MemAnalyzer is used from an external script to dump several processes
        /// at one time we will get distinct times in the CSV output which makes it harder to group all processes from one snapshot 
        /// together in Excel Charts to get a trend over time between snapshots of a collection of processes.
        /// </summary>
        public string ExternalTime { get; set; }

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

        public static string GetProcessName(int pid)
        {
            return Process.GetProcessById(pid).ProcessName;
        }

        public static string GetProcessCmdLine(int pid)
        {
            string cmdLine = ProcessFilter.GetProcessCommandLine(pid);
            return cmdLine.Replace('"', ' ')
                          .Replace('\t', ' ');
        }

        public bool IsLiveProcess
        {
            get => Pid1 != 0;
        }

        public DateTime CurrentTimeOrDumpCreationDate
        {
            get
            {
                DateTime lret = DateTime.Now;
                if( !IsLiveProcess )
                {
                    lret = new FileInfo(DumpFileName1).CreationTime;
                }

                return lret;
            }
        }

        Process _Pid1Process;

        int _AgeInSeconds = -1;

        /// <summary>
        /// Get from process with Pid1 the age in seconds. 0 if pid1 is 0.
        /// </summary>
        public int ProcessAgeInSeconds
        {
            get
            {
                if( _Pid1Process == null && Pid1 != 0)
                {
                    _Pid1Process = Process.GetProcessById(Pid1);
                }

                if( _AgeInSeconds == -1)
                {
                    _AgeInSeconds = _Pid1Process == null ? 0 : (int)((DateTime.Now - _Pid1Process.StartTime).TotalSeconds);
                }

                return _AgeInSeconds;
            }
        }


        string _Process1CommandLine;
        string Process1CommandLine
        {
            get
            {
                if(_Process1CommandLine == null)
                {
                    if (Pid1 != 0)
                    {
                        _Process1CommandLine = ProcessFilter.GetProcessCommandLine(Pid1);
                    }
                    else
                    {
                        _Process1CommandLine = "";
                    }
                }

                return _Process1CommandLine;
            }
        }

        /// <summary>
        /// Get Process Name of process with Pid1. Otherwise the process dump file name is returned.
        /// </summary>
        public string ProcessName
        {
            get
            {
                if (_Pid1Process == null && Pid1 != 0)
                {
                    _Pid1Process = Process.GetProcessById(Pid1);
                }

                return _Pid1Process != null ? Renamer.Rename(_Pid1Process.ProcessName, Process1CommandLine) :
                                             Path.GetFileName(DumpFileName1);
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

        /// <summary>
        /// Load the executable name and command line substring file to rename processes based on their arguments.
        /// </summary>
        /// <param name="processRenameFile">Can be null. In that case no process renaming takes place.</param>
        internal void LoadProcessRenameFile(string processRenameFile)
        {
            if (String.IsNullOrEmpty(processRenameFile))
            {
                Renamer = new ProcessRenamer();
            }
            else if (!File.Exists(processRenameFile))
            {
                Renamer = new ProcessRenamer();
                if (!Program.IsChild)
                {
                    Console.WriteLine($"Warning: Process rename file {processRenameFile} was not found!");
                }
            }
            else
            {
                XmlSerializer ser = new XmlSerializer(typeof(ProcessRenamer));
                Renamer = (ProcessRenamer)ser.Deserialize(new StreamReader(processRenameFile));
            }
        }
    }
}
