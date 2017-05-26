using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemAnalyzer
{
    /// <summary>
    /// Rename an executable based on executable name and command line.
    /// This is useful if you have generic container processes (like w3wp.exe) where many instances of them are running which 
    /// run different code.
    /// </summary>
    public class ProcessRenamer
    {
        List<RenameRule> _ProcessRenamers = new List<RenameRule>();
        Dictionary<KeyValuePair<string,string>, string> _RenameCache = new Dictionary<KeyValuePair<string, string>, string>();

        public List<RenameRule> ProcessRenamers
        {
            get => _ProcessRenamers;
            set => _ProcessRenamers = value ?? new List<RenameRule>();
        }

        /// <summary>
        /// Rename the process to give the logs a more descriptive name.
        /// </summary>
        /// <param name="exeName">Executable to check</param>
        /// <param name="cmdArgs">Command line arguments of this executable</param>
        /// <returns>exeName if no rename rule exist or descriptive name.</returns>
        public string Rename(string exeName, string cmdArgs)
        {
            string renamed = exeName;

            var key = new KeyValuePair<string, string>(exeName, cmdArgs);

            if (_RenameCache.TryGetValue(key, out string cached))
            {
                renamed = cached;
            }
            else
            {
                foreach (var renameOp in this._ProcessRenamers)
                {
                    renamed = renameOp.Rename(exeName, cmdArgs);
                    if (renamed != exeName)
                    {
                        break;
                    }
                }
                _RenameCache[key] = renamed;
            }

            return renamed;
        }

        public class RenameRule
        {
            string _ExeName;
            public string ExeName
            {
                get => _ExeName ?? "";
                set => _ExeName = value;
            }

            List<string> _CmdLineSubstrings;

            public List<string> CmdLineSubstrings
            {
                get
                {
                    _CmdLineSubstrings = _CmdLineSubstrings == null ? new List<string>() : _CmdLineSubstrings;
                    return _CmdLineSubstrings;
                }
                set
                {
                    _CmdLineSubstrings = value ?? new List<string>();
                }
            }

            List<string> _NotCmdLineSubstrings;

            public List<string> NotCmdLineSubstrings
            {
                get
                {
                    _NotCmdLineSubstrings = _NotCmdLineSubstrings == null ? new List<string>() : _NotCmdLineSubstrings;
                    return _NotCmdLineSubstrings;
                }
                set
                {
                    _NotCmdLineSubstrings = value ?? new List<string>();
                }
            }

            string _NewExeName;
            public string NewExeName
            {
                get => _NewExeName ?? "";
                set => _NewExeName = value ?? "";
            }


            public RenameRule()
            { }

            public RenameRule(string exeName, List<string> cmdLineSubStrings, List<string> notCmdlineStrings, string newExeName)
            {
                ExeName = exeName ?? "";
                CmdLineSubstrings = cmdLineSubStrings;
                NotCmdLineSubstrings = notCmdlineStrings;
                NewExeName = newExeName ?? "";
            }

            /// <summary>
            /// Rename exe where 
            /// </summary>
            /// <param name="exeName"></param>
            /// <param name="cmdLine"></param>
            /// <returns></returns>
            public string Rename(string exeName, string cmdLine)
            {
                string lret = exeName;
                if (ExeName.Equals(exeName, StringComparison.OrdinalIgnoreCase))
                {
                    bool hasMatches = CmdLineSubstrings.All(substr => cmdLine.IndexOf(substr, StringComparison.OrdinalIgnoreCase) != -1);

                    // empty filter counts a no filter
                    if (CmdLineSubstrings.Count == 0)
                    {
                        hasMatches = true;
                    }
                    bool hasNotMatches = NotCmdLineSubstrings.Where(x=> !String.IsNullOrEmpty(x)).Any(substr => cmdLine.IndexOf(substr, StringComparison.OrdinalIgnoreCase) != -1);

                    if (hasMatches && !hasNotMatches)
                    {
                        lret = NewExeName;
                    }
                }

                return lret;
            }
        }

    }
}
