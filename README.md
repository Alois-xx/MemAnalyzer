# MemAnalyzer
is a command line memory analysis tool for managed code.
It can show which objects use most space on the managed heap just like !DumpHeap from Windbg without the need to install and attach a debugger. 

# Features

- Single self contained executable
- Supports x86 and x64 processes and memory dumps
- Memory dump diff
- Optional CSV output 

# Examples
### Dump objects by size (-dts dd)
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

### Dump objects by count (-dtn dd)
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

	MemAnalyzer 2.0.0.0 by Alois Kraus 2017
	Usage: MemAnalyzer [ -f DumpFile or -pid ddd [ -f2 DumpFile or -pid2 ddd ] -dts [N] or -dtn [N] or -dstring [N] [-live] ] [-gc xxx [-process xxx.exe]] [-o Output.csv [-sep     ]]
		   -f fileName          Dump file to analyze.
		   -f2 fileName         Diff Dump files
		   -pid ddd             Live process to analyze.
		   -pid2 ddd            Live process to diff. You can combined it to e.g. compare a live process with a dump. Subtraction is done -xxx2 - xxxx where xxx is Pid or f
		   -dts N               Dump top N types by object size. Default for N is 20.
		   -dtn N               Dump top N types by object count. Default for N is 20.
		   -dstrings N          Dump top N duplicate strings and global statistics. Default for N is 20.
		   -live                If present only reachable (live) objects are considered in the statistics. Takes longer to calculate.
		   -gc xxx or ""        Force GC in process with id or if xxx is not a number it is treated as a command line substring filter. E.g. -forceGC GenericReader
								will force a GC in all generic reader processes. Use "" as filter if you use -process to force a GC in all executables.
		   -process xxx.exe     (optional) Name of executable in which a GC should happen. Must contain .exe in its name.
		   -o output.csv        Write output to csv file instead of console
	Examples
	Dump types by size from dump file.
			MemAnalyzer -f xx.dmp -dts
	Dump types by object count from a running process with process id ddd.
			MemAnalyzer -pid ddd -dts
	Diff two memory dump files where (f2 - f) are calculated.
			MemAnalyzer -f dump1.dmp -f2 dump2.dmp -dts
	Dump string duplicates of live process and write it to CSV file
			MemAnalyzer -pid ddd -dstrings -o StringDuplicates.csv

