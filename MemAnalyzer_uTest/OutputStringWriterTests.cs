using System;
using MemAnalyzer;
using System.IO;
using System.Text;
using Xunit;

namespace MemAnalyzer_uTest
{
    public class OutputStringWriterTests
    {
        [Fact]
        public void Throw_On_Null_Fmt()
        {
            Assert.Throws<ArgumentNullException>(() => OutputStringWriter.Format(null, " "));
        }

        [Fact]
        public void Use_StringFormat_As_Default()
        {
            const string fmt = "{0:N0} {1:N0}";
            const int Million = 1000 * 1000;
            string millionFormatted = String.Format(fmt, Million, Million);
            OutputStringWriter.CsvOutput = false;
            Assert.Equal(millionFormatted, OutputStringWriter.Format(fmt, Million, Million));
        }

        [Fact]
        public void Separate_Columns_By_Tabs_When_CSV_Output_Enabled()
        {
            const string fmt = "{0:N0} {1:N0}";
            const int Million = 1000 * 1000;
            string millionFormatted = String.Format(fmt, Million, Million);
            OutputStringWriter.CsvOutput = true;
            Assert.Equal("1000000\t1000000", OutputStringWriter.Format(fmt, Million, Million));
        }

        [Fact]
        public void Can_Write_To_File()
        {
            MemoryStream stream = new MemoryStream();
            OutputStringWriter.CsvOutput = false;
            using (StreamWriter writer = new StreamWriter(stream))
            {
                OutputStringWriter.Output = writer;
                OutputStringWriter.FormatAndWrite("{0},{1}",1,2);
            }

            string output = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal("1,2"+Environment.NewLine, output);


            stream = new MemoryStream();
            using (StreamWriter writer = new StreamWriter(stream))
            {
                OutputStringWriter.Output = writer;
                OutputStringWriter.FormatAndWrite("{0},{1}", 3, 4);
            }

            string output2 = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Equal("3,4" + Environment.NewLine, output2);

        }
    }
}
