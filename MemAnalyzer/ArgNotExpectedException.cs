using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemAnalyzer
{
    [Serializable]
    class ArgNotExpectedException : ArgumentException
    {
        public ArgNotExpectedException(string message):base(message)
        { }
    }
}
