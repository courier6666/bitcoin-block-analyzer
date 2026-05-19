using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OutputScriptTypeBlockAnalyzer.Core
{
    public enum ScriptOpcodes
    {
        OP_PUSHBYTES_33 = 0x21,
        OP_PUSHBYTES_65 = 0x41,
        OP_PUSHBYTES_20 = 0x14,
        OP_PUSHBYTES_32 = 0x20,
        OP_PUSHBYTES_11 = 0x0b,
        OP_PUSHBYTES_75 = 0x4b,
        OP_CHECKSIG = 0xac,
        OP_HASH160 = 0xa9,
        OP_RETURN = 0x6a,
        OP_EQUALVERIFY = 0x88,
        OP_EQUAL = 0x87,
        OP_DUP = 0x76,
        OP_0 = 0x00,
        OP_1 = 0x51,
        OP_2 = 0x52,
        OP_16 = 0x60,
        OP_CHECKMULTISIG = 0xae,
    }
}