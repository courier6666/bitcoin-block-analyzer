using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OutputScriptTypeBlockAnalyzer.Core
{
    public class Transaction
    {
        public string TransactionId { get; set; } = default!;

        public OutputScriptPubKeyType[] OutputScripts { get; set; } = default!;
    }
}
