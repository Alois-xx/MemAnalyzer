using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Text;

namespace MemAnalyzer
{

    /// <summary>
    /// Filter which matches one or more processes by executable name and command line or only via exe name or via pid.
    /// </summary>
    public class ProcessFilter
    {
        /// <summary>
        /// Substring filter which the process name must match. If null this filed is ignored.
        /// </summary>
        public string ProcessNameFilter
        {
            get;
            internal set;
        }

        /// <summary>
        /// Contains a list of substring which each matching process must contain. Multiple substring form an AND condition.
        /// </summary>
        public string[] CmdLineFilters
        {
            get;
            internal set;
        }

        /// <summary>
        /// If this field is set the process is selected by its pid which ensures that this can match only one or more processes. 
        /// </summary>
        public int PidFilter
        {
            get;
            internal set;
        }

        /// <summary>
        /// Create a filter where the process id is already kown.
        /// </summary>
        /// <param name="pidFilter">Pid of process to search </param>
        public ProcessFilter(int pidFilter):this(pidFilter, null,null)
        {
        }

        /// <summary>
        /// Select all processes which match this process name.
        /// </summary>
        /// <param name="processNameFilter">Process name to select. E.g. Notepad.exe matching is case insensitive.</param>
        public ProcessFilter(string processNameFilter)
            : this(0, processNameFilter, null)
        { }

        /// <summary>
        /// Select all processes which match the process name and the corresponding command line substrings.
        /// </summary>
        /// <param name="processNameFilter">Process name to select. E.g. Notepad.exe matching is case insensitive.</param>
        /// <param name="cmdLine">Substring command line filter.</param>
        public ProcessFilter(string processNameFilter, string cmdLine):this(0, processNameFilter, new string[] { cmdLine }) 
        { }

        /// <summary>
        /// Select all processes which match the process name and the corresponding command line substrings. Multiple cmd line filter strings
        /// form an and condition for one process command line to create a match. 
        /// </summary>
        /// <param name="processNameFilter">Process name to select. E.g. Notepad.exe matching is case insensitive.</param>
        /// <param name="cmdLineFilters">Substring command line filters.</param>
        public ProcessFilter(string processNameFilter, string[] cmdLineFilters)
            : this(0, processNameFilter, cmdLineFilters)
        { }

        private ProcessFilter(int pidFilter, string processNameFilter, string[] cmdLineFilters)
        {
            PidFilter = pidFilter;
            ProcessNameFilter = processNameFilter;
            CmdLineFilters = cmdLineFilters;
        }


        /// <summary>
        /// Print the process filter in a human readable form
        /// </summary>
        /// <returns>String representation of this instance.</returns>
        public override string ToString()
        {
            string lret = "";
            if( PidFilter != 0 )
            {
                lret += "PidFilter: " + PidFilter + " ";
            }

            if( ProcessNameFilter != null )
            {
                lret += "ProcessNameFilter: " + ProcessNameFilter + " ";
            }

            if( CmdLineFilters != null )
            {
                foreach(var cmdFilter in CmdLineFilters)
                {
                    lret += "CmdFilter: \"" + cmdFilter + "\" ";
                }
            }

            return lret;
        }

        /// <summary>
        /// Get from a running process the command line via WMI. Please note that the command line is not complete. Only the first 60 
        /// characters are returned by WMI.
        /// </summary>
        /// <param name="pid">Process Id of process to fetch the command line.</param>
        /// <returns>Read command line or an empty string if no process could be found.</returns>
        public static string GetProcessCommandLine(int pid)
        {
            string query = $"SELECT Name, CommandLine, ProcessId FROM Win32_Process WHERE ProcessId='{pid}'";
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
            foreach(ManagementObject mo in searcher.Get())
            {
                return (string)mo["CommandLine"] ?? "";
            }

            return "";
        }

        /// <summary>
        /// Query the system for all matching processes which are defined by this filter.
        /// </summary>
        /// <returns>Process ids of the matching processes.</returns>
        public IEnumerable<int> GetMatchingPids()
        {
            string query = "SELECT Name, CommandLine, ProcessId FROM Win32_Process";
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
            foreach (ManagementObject mo in searcher.Get())
            {
                string exename = (string) mo["Name"];
                string cmdLine = (string) mo["CommandLine"];
                int pid = (int) (UInt32) mo["ProcessId"];

                if( CmdLineFilters != null )
                {
                    if( String.IsNullOrEmpty(cmdLine) )
                    {
                        continue;
                    }

                    foreach(var cmdLineFilter in CmdLineFilters)
                    {
                        if (cmdLine.IndexOf(cmdLineFilter, StringComparison.OrdinalIgnoreCase) == -1)
                        {
                            goto Continue;
                        }

                    }
                }

                if( PidFilter != 0 )
                {
                    if( PidFilter != pid)
                    {
                        continue;
                    }
                }

                if( !String.IsNullOrEmpty(ProcessNameFilter)  )
                {
                    if( String.Compare(ProcessNameFilter, exename, StringComparison.OrdinalIgnoreCase) != 0 )
                    {
                        continue;
                    }
                }

                yield return pid;

            Continue:
                ;
            }
        }
    }
}
