using System;

namespace MemAnalyzer
{
    internal class ProcessContextInfo
    {
        private int pid;
        private DateTime time;
        private string commandLine;
        private int ageIns;
        private string name;
        private string context;

        public int Pid { get => pid; set => pid = value; }
        public DateTime Time { get => time; set => time = value; }
        public string CommandLine { get => commandLine; set => commandLine = value; }
        public int AgeIns { get => ageIns; set => ageIns = value; }
        public string Name { get => name; set => name = value; }
        public string Context { get => context; set => context = value; }
    }
}
