using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OutputScriptTypeBlockAnalyzer.Core
{
    public class Output
    {
        public string Script { get; set; } = default!;

        public OutputScriptPubKeyType ScriptType { get; set; }
    }
}
