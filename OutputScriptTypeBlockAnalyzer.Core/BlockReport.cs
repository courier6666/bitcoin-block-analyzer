using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OutputScriptTypeBlockAnalyzer.Core
{
    public class BlockReport
    {
        public int Height { get; set; }

        public string Hash { get; set; } = default!;

        public Transaction[] Transactions { get; set; } = default!;
    }
}
