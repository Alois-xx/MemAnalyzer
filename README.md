# MemAnalyzer
is a command line memory analysis tool for managed code.
It can show which objects use most space on the managed heap just like !DumpHeap from Windbg without the need to install and attach a debugger. 

# Features

- Single self contained executable
- Supports x86 and x64 processes and memory dumps
- Create memory dumps with associated VMMap data
- Analyze managed heap, unmanaged, private bytes and file mappings when VMMap is present.
- Memory dump diff
- Optional CSV output 

# Examples
### Dump Types by size (-dts dd)
*C>MemAnalyzer.exe -dts -pid 1234*

		Allocated(Bytes)        Instances(Count)        Type
		2,529,742               61,330                  System.String
		918,928                 23                      System.String[]
		267,648                 11,152                  System.Int32
		35,336                  9                       System.Object[]
		3,248                   58                      System.RuntimeType
		1,072                   2                       System.Globalization.CultureData
		764                     3                       System.Char[]
		732                     12                      System.Int32[]
		432                     1                       System.Collections.Generic.Dictionary+Entry<System.Type,System.Security.Policy.EvidenceTypeDescriptor>[]
		432                     2                       System.Globalization.NumberFormatInfo
		320                     2                       System.Threading.ThreadAbortException
		3,808,968                                       Total(Heap Size)
		44,922                                          Total(Free)
		3,764,046               72,671                  Total(Allocated)

### Dump Types by count (-dtn dd)
*C>MemAnalyzer.exe -dtn -f 50KStringsx64.dmp*

		Allocated(Bytes)        Instances(Count)        Type
		2,529,742               61,330                  System.String
		267,648                 11,152                  System.Int32
		3,248                   58                      System.RuntimeType
		918,928                 23                      System.String[]
		732                     12                      System.Int32[]
		35,336                  9                       System.Object[]
		144                     6                       System.Object
		96                      4                       System.UInt16
		3,808,968                                       Total(Heap Size)
		44,922                                          Total(Free)
		3,764,046               72,671                  Total(Allocated)

### Show String Waste Statistics (-dts dd)
*C>MemAnalyzer.exe -dstrings -f 50KStringsx64.dmp*

		Strings(Count)  Waste(Bytes)    String
		500             20,958          String 0
		500             20,958          String 1
		500             20,958          String 2
		500             20,958          String 3
		500             20,958          String 4
		500             20,958          String 5

	Summary
	==========================================
	Strings                       61,330 count
	Allocated Size             2,529,742 bytes
	Waste Duplicate Strings    2,515,898 bytes

### Diff Two dump files and show diff by object size
*C>MemAnalyzer.exe  -dts -f 50KStringsx64.dmp -f2 Dumps\Emptyx64.dmp*

		Delta(Bytes)    Delta(Instances         Instances       Instances2      Allocated(Bytes)        Allocated2(Bytes)       AvgSize(Bytes)  AvgSize2(Bytes) Type
		-2,522,006      -61,161                 61,330          169             2,529,742               7,736                   41              45              System.String
		-917,632        -4                      23              19              918,928                 1,296                   39953           68              System.String[]
		-267,576        -11,149                 11,152          3               267,648                 72                      24              24              System.Int32
		360             1                       0               1               0                       360                                     360             System.Reflection.CustomAttributeRecord[]
		-3,705,105      -72,283                 72,671          388             3,764,046               58,941                  N.A.            N.A.            Total

### Write output to csv file
*C>MemAnalyzer.exe  -dts -f 50KStringsx64.dmp -o test.csv*

	Writing output to csv file test22.csv

## Command Line Help

MemAnalyzer 2.5.0.0 by Alois Kraus 2017
Usage:
       MemAnalyzer [ -f DumpFile or -pid ddd [ -f2 DumpFile or -pid2 ddd ] -dts [N] or -dtn [N] or -dstrings [N] [-live] [-unit DisplayUnit] [-vmmap] ]
                   [-o Output.csv [-sep \t] [-noexcelsep] [-time "externalTime"]]
                   [ [-verifydump] -procdump pidOrExe [outputDumpFileOrDir]]
       -f fileName          Dump file to analyze.
       -f2 fileName         Second dump file to diff.
       -pid ddd             Live process to analyze.
       -pid2 ddd            Second live process to diff. You can also mix to compare e.g. a dump and a live process e.g. -pid2 ddd -f dump.dmp
       -vmmap               Fetch from live processes VMMAP data. VMMap.exe must be in the path to work.
       -dts N               (default) Dump top N types by object size. Default for N is 20.
       -dtn N               Dump top N types by object count. Default for N is 20.
                            N can be a number or ddd;N#dd where the first number ddd is a threshold value to consider only types with an instance count greater ddd. The second number is the TopN number
       -dstrings N          Dump top N duplicate strings and global statistics. Default for N is 20.
       -showAddress         Used together with -dstrings. Show the address of one string of a given value
       -unit DisplayUnit    DisplayUnit can be Bytes, KB, MB or GB
       -live                If present only reachable (live) objects are considered in the statistics. Takes longer to calculate.
       -dacdir dir          If the dump file is from a machine with a different version you can tell MemAnalyzer in which directory to search for matching dac dlls.
                            See https://1drv.ms/f/s!AhcFq7XO98yJgoMwuPd7LNioVKAp_A for a collection of dac dlls from .NET 2.0 up to 4.7.
       -silent              Suppress information messages
 Dump Creation:
       -procdump args       Create a memory dump and VMMap snapshot of a process. Needs procdump.exe and vmmap.exe in the path to work
       -verifydump          Used with -procdump. This checks the managed heap for consistency to be sure that it can be loaded later
 CSV Output
       -o output.csv        Write output to csv file instead of console
       -overwrite           Overwrite CSV output if file already exist. Otherwise it is appended
       -timefmt "xxx"       xxx can be Invariant or a .NET DateTime format string for CSV output. See https://msdn.microsoft.com/en-us/library/8kb3ddd4(v=vs.110).aspx
       -time    "Mon..."    Supply an external time string which is put into the Time column of the CSV output. This helps to group by time for a specific snapshot in Excel because otherwise MemAnalyzer will
                            always use the current time for each process which is dumped. That makes it harder to filter by time for for a bunch of observed processes
       -context "xxx"       Additional context which is added to the context column. Useful for test reporting to e.g. add test run number to get a metric how much it did leak per test run
       -sep "x"             CSV separator character. Default is tab
       -noexcelsep          By default write sep= to make things easier when working with Excel. When set sep= is not added to CSV output
       -renameProc xxx.xml  Optional xml file which contains executable and command line substrings to rename processes based on their command line to get better names
 Return Value: If -dts/dtn is used it will return the allocated managed memory in KB
               If additionally -vmmap is present it will return allocated Managed Heap + Heap + Private + Shareable + File Mappings
Examples
Dump types by size from dump file which was created with a different .NET Version
        MemAnalyzer -f xx.dmp -dts -dacdir c:\mscordacwks
Dump types by object count from a running process with process id ddd.
        MemAnalyzer -pid ddd -dtn
Diff two memory dump files where (f2 - f) is shown. VMMap information is also used to (see -procdump) to diff unmanaged heap and other memory types
        MemAnalyzer -f dump1.dmp -f2 dump2.dmp -dts -vmmap
Dump string duplicates of live process and write it to CSV file
        MemAnalyzer -pid ddd -dstrings -o StringDuplicates.csv
Create a full process dump along with VMMap information if VMMap is in the path. Procdump will expand PROCESSNAME, PID, ... by itself
        MemAnalyzer -procdump -ma pid C:\temp\PROCESSNAME_PID_YYMMDD_HHMMSS.dmp


