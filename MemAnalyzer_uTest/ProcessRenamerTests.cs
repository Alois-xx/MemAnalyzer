using MemAnalyzer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace MemAnalyzer_uTest
{
    [TestClass]
    public class ProcessRenamerTests
    {
        [TestMethod]
        public void Can_Round_Trip()
        {
            XmlSerializer ser = new XmlSerializer(typeof(ProcessRenamer));
            ProcessRenamer renamer = new ProcessRenamer();
            renamer.ProcessRenamers.Add(new ProcessRenamer.RenameRule("w3wp.exe", new List<string> { "First" }, new List<string> { "Not" }, "NewW3wp.exe"));

            var mem = new MemoryStream();

            ser.Serialize(mem, renamer);

            mem.Position = 0;
            Console.WriteLine(Encoding.UTF8.GetString(mem.ToArray()));
            ProcessRenamer renNew = (ProcessRenamer)ser.Deserialize(mem);

            Assert.AreEqual(renamer.ProcessRenamers.Count, renNew.ProcessRenamers.Count);
            Assert.AreEqual(renamer.ProcessRenamers[0].CmdLineSubstrings.Count, renNew.ProcessRenamers[0].CmdLineSubstrings.Count);
            Assert.AreEqual(renamer.ProcessRenamers[0].CmdLineSubstrings[0], renNew.ProcessRenamers[0].CmdLineSubstrings[0]);
            Assert.AreEqual(renamer.ProcessRenamers[0].ExeName, renNew.ProcessRenamers[0].ExeName);
            Assert.AreEqual(renamer.ProcessRenamers[0].NewExeName, renNew.ProcessRenamers[0].NewExeName);
            Assert.AreEqual(renamer.ProcessRenamers[0].NotCmdLineSubstrings.Count, renNew.ProcessRenamers[0].NotCmdLineSubstrings.Count);
            Assert.AreEqual(renamer.ProcessRenamers[0].NotCmdLineSubstrings[0], renNew.ProcessRenamers[0].NotCmdLineSubstrings[0]);
        }

        [TestMethod]
        public void Keep_Name_When_Empty()
        {
            ProcessRenamer ren = new ProcessRenamer();

            Assert.AreEqual("exe", ren.Rename("exe", "args"));
        }

        [TestMethod]
        public void Substring_Match_Forces_Rename()
        {
            ProcessRenamer ren = new ProcessRenamer();
            ren.ProcessRenamers.Add(new ProcessRenamer.RenameRule("exe", new List<string> { "ind" }, null, "NewExe"));

            Assert.AreEqual("NewExe", ren.Rename("exe", "-index"));
            Assert.AreEqual("somethingElse", ren.Rename("somethingElse", "-index"));
        }

        [TestMethod]
        public void Substring_Match_Is_And_Wise()
        {
            ProcessRenamer ren = new ProcessRenamer();
            ren.ProcessRenamers.Add(new ProcessRenamer.RenameRule("exe", new List<string> { "-index", "-second" }, null, "NewExe"));

            Assert.AreEqual("exe", ren.Rename("exe", "-index"));
            Assert.AreEqual("exe", ren.Rename("exe", "-second"));
            Assert.AreEqual("NewExe", ren.Rename("exe", "-second -index"));
        }

        [TestMethod]
        public void Substring_Not_Match_Is_Or_Wise()
        {
            ProcessRenamer ren = new ProcessRenamer();
            ren.ProcessRenamers.Add(new ProcessRenamer.RenameRule("exe", new List<string> { "-i", "-s" }, 
                new List<string> { "-index", "-second" }, "NewExe"));

            Assert.AreEqual("NewExe", ren.Rename("exe", "-i -s"));
            Assert.AreEqual("NewExe", ren.Rename("exe", "-i -s"));
            Assert.AreEqual("exe", ren.Rename("exe", "-i -s -index"));
            Assert.AreEqual("exe", ren.Rename("exe", "-i -s -second"));
            Assert.AreEqual("exe", ren.Rename("exe", "-i -s -second -index"));

        }
    }
}
