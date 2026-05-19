using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OutputScriptTypeBlockAnalyzer.Core
{
    public enum OutputScriptPubKeyType
    {
        P2TR,
        P2WPKH,
        P2SH,
        P2PKH,
        OP_RETURN,
        P2WSH,
        Multisig,
        P2PK,
        Nonstandard
    }
}
