using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemAnalyzer
{
    /// <summary>
    /// Write a formatted string to Console.Out or a text files with \t in a CSV format.
    /// </summary>
    internal static class OutputStringWriter
    {
        /// <summary>
        /// If true the format string of Write is ignored and instead the values are printed out in columns separated by the SeparatorChar
        /// </summary>
        public static bool CsvOutput
        {
            get;set;
        }

        static TextWriter _Output;

        /// <summary>
        /// Default is Console.Out, if set to some other value you can flush it later.
        /// </summary>
        public static TextWriter Output
        {
            get
            {
                if (_Output == null)
                {
                    _Output = Console.Out;
                }
                return _Output;
            }

            set
            {
                _Output = value;
            }
        }

        static char _SeparatorChar = '\t';

        /// <summary>
        /// Column separator character for CSV output. Default is \t
        /// </summary>
        public static char SeparatorChar
        {
            get => _SeparatorChar;
            set => _SeparatorChar = value;
        }
        public static bool SuppressHeader { get; internal set; }

        /// <summary>
        /// see https://superuser.com/questions/407082/easiest-way-to-open-csv-with-commas-in-excel why this was added.
        /// </summary>
        public static bool DisableExcelCSVSep { get; internal set; }

        public static void Flush()
        {
            Output.Flush();
        }

        /// <summary>
        /// Write a formatted string. 
        /// </summary>
        /// <param name="fmt">Use String.Format if <see cref="CsvOutput"/> is false. Otherwise the <see cref="args"/>are formatted with <see cref="SeparatorChar"/></param>
        /// <param name="args"></param>
        /// <returns></returns>
        internal static string Format(string fmt, params object[] args)
        {
            if (String.IsNullOrEmpty(fmt))
            {
                throw new ArgumentNullException("fmt");
            }

            if (args == null || args.Length == 0)
            {
                return fmt;
            }

            if (CsvOutput)
            {
                StringBuilder sb = new StringBuilder();
                foreach (var arg in args)
                {
                    sb.Append(arg);
                    sb.Append(SeparatorChar);
                }

                return sb.ToString(0, sb.Length - 1);
            }
            else
            { 
                return String.Format(fmt, args);
            }
        }

        public static void FormatAndWrite(string fmt, params object[] args)
        {
            Output.WriteLine(Format(fmt, args));
        }

        public static void FormatAndWriteHeader(string fmt, params object[] args)
        {
            if( !SuppressHeader )
            {
                if (!DisableExcelCSVSep && CsvOutput)
                {
                    FormatAndWrite($"sep={SeparatorChar}");
                }
                FormatAndWrite(fmt, args);
            }
        }
    }
}
