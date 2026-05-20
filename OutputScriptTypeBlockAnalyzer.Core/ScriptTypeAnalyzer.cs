using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OutputScriptTypeBlockAnalyzer.Core
{
    public class ScriptTypeAnalyzer
    {
        private readonly byte[] _blockRawData;

        private ScriptTypeAnalyzer(byte[] blockRawData)
        {
            _blockRawData = blockRawData;
        }

        public static async Task<ScriptTypeAnalyzer> CreateAsync(string blockPath)
        {
            
            string hexBlockData = await File.ReadAllTextAsync(blockPath);
            string newLineSuffix = hexBlockData[^2..];
            
            if (newLineSuffix == "\r\n")
            {
                hexBlockData = hexBlockData[..^2];
            }

            byte[] blockRawData = Convert.FromHexString(hexBlockData);
            return new ScriptTypeAnalyzer(blockRawData);
        }

        public BlockReport Analyze()
        {
            using var stream = new MemoryStream(_blockRawData);

            //reading header to generate block hash
            Span<byte> headerBytes = stackalloc byte[80];
            stream.ReadExactly(headerBytes);
            var blockHash = GetBlockHashHex(headerBytes);
            
            // reading transaction count
            var transactionCount = ReadCompactSize(stream);
            var blockReport = new BlockReport
            {
                Height = (int)GetBlockHeight(stream),
                Hash = blockHash,
                Transactions = new Transaction[transactionCount],
            };

            for (ulong i = 0; i < transactionCount; i++)
            {
                blockReport.Transactions[i] = AnalyzeTransaction(stream);
            }

            return blockReport;
        }

        private static ulong GetBlockHeight(Stream stream) // must begin at position of first ransaction
        {
            int lastPosition = (int)stream.Position;

            // skipping version
            SkipBytes(stream, 4);

            // peek at the next byte to detect SegWit
            int firstByte = stream.ReadByte();


            if (firstByte == 0x00)
            {
                int flag = stream.ReadByte();
                if (flag != 0x01)
                {
                    throw new InvalidOperationException("Invalid SegWit transaction flag.");
                }
            }
            else
            {
                stream.Seek(-1, SeekOrigin.Current);
            }

            //input count discarded
            _ = ReadCompactSize(stream);

            // reading first input
            // skipping transaction id
            SkipBytes(stream, 32);

            // skipping vout
            SkipBytes(stream, 4);

            // scriptSig size discarded
            _ = ReadCompactSize(stream);

            // read first byte to determine byte count of block height in coinbase scriptSig
            var heightByteCount = stream.ReadByte();
            Span<byte> heightRaw = stackalloc byte[heightByteCount];
            stream.ReadExactly(heightRaw);

            stream.Position = lastPosition;
            return ConvertToUInt64WithPadding(heightRaw);
        }

        private static string GetBlockHashHex(Span<byte> header)
        {
            if (header.Length != 80)
            {
                throw new ArgumentException("Invalid block header length. Expected 80 bytes.", nameof(header));
            }

            Span<byte> data = SHA256.HashData(SHA256.HashData(header));
            data.Reverse();

            return Convert.ToHexString(data).ToLower();
        }

        private static Transaction AnalyzeTransaction(Stream stream)
        {
            // transaction hash
            var transactionId = GetTransactionHashHex(stream);

            // skip transaction version
            SkipBytes(stream, 4);

            // peek at the next byte to detect SegWit
            int firstByte = stream.ReadByte();

            bool isSegWit = false;

            if (firstByte == 0x00)
            {
                int flag = stream.ReadByte();
                if (flag != 0x01)
                {
                    throw new InvalidOperationException("Invalid SegWit transaction flag.");
                }
                isSegWit = true;
            }
            else
            {
                stream.Seek(-1, SeekOrigin.Current);
            }

            var inputCount = ReadCompactSize(stream);
            for (ulong i = 0; i < inputCount; ++i)
            {
                SkipInput(stream);
            }

            var outputCount = ReadCompactSize(stream);
            var transaction = new Transaction
            {
                TransactionId = transactionId,
                Outputs = new Output[outputCount],
            };

            for (ulong i = 0; i < outputCount; ++i)
            {
                transaction.Outputs[i] = AnalyzeOutput(stream);
            }

            // Only skip witness data if this is a SegWit transaction
            if (isSegWit)
            {
                SkipWitnessData(stream, inputCount);
            }

            // skipping lock time
            SkipBytes(stream, 4);

            return transaction;
        }

        private static string GetTransactionHashHex(Stream stream)
        {
            // saving current position to read transaction data for hashing
            var position = (int)stream.Position;

            // reading version
            Span<byte> versionBytes = stackalloc byte[4];
            stream.ReadExactly(versionBytes);

            // peek at the next byte to detect SegWit
            int firstByte = stream.ReadByte();
            bool isSegWit = false;

            if (firstByte == 0x00)
            {
                int flag = stream.ReadByte();
                if (flag != 0x01)
                {
                    throw new InvalidOperationException("Invalid SegWit transaction flag.");
                }

                isSegWit = true;
            }
            else
            {
                stream.Seek(-1, SeekOrigin.Current);
            }

            // reading inputs and outputs
            var inputCountStartPosition = (int)stream.Position;
            
            var inputCount = ReadCompactSize(stream);

            for (ulong i = 0; i < inputCount; ++i)
            {
                SkipInput(stream);
            }

            var outputCount = ReadCompactSize(stream);

            for (ulong i = 0; i < outputCount; ++i)
            {
                SkipOutput(stream);
            }

            var outputsEndPosition = (int)stream.Position - 1;
            var inputAndOutputsSize = outputsEndPosition - inputCountStartPosition + 1;
            Span<byte> transactionData = stackalloc byte[inputAndOutputsSize];

            stream.Position = inputCountStartPosition;
            stream.ReadExactly(transactionData);

            // skipping segwit
            if (isSegWit)
            {
                SkipWitnessData(stream, inputCount);
            }

            Span<byte> lockTimeBytes = stackalloc byte[4];
            stream.ReadExactly(lockTimeBytes);

            Span<byte> dataToHash = stackalloc byte[versionBytes.Length + transactionData.Length + lockTimeBytes.Length];
            versionBytes.CopyTo(dataToHash);
            transactionData.CopyTo(dataToHash[versionBytes.Length..]);
            lockTimeBytes.CopyTo(dataToHash[^lockTimeBytes.Length..]);

            var hash = SHA256.HashData(SHA256.HashData(dataToHash));
            hash.AsSpan().Reverse();

            stream.Position = position;
            return Convert.ToHexString(hash).ToLower();
        }

        private static void SkipInput(Stream stream)
        {
            // skipping transaction id
            SkipBytes(stream, 32);

            // skipping vout
            SkipBytes(stream, 4);

            var scriptSigSize = ReadCompactSize(stream);

            // skipping scriptSig
            SkipBytes(stream, (int)scriptSigSize);

            // skipping sequence
            SkipBytes(stream, 4);
        }

        private static void SkipOutput(Stream stream)
        {
            // skipping Amount
            SkipBytes(stream, 8);

            var scriptPubKeySize = ReadCompactSize(stream);
            // skipping scriptPubKey
            SkipBytes(stream, (int)scriptPubKeySize);
        }

        private static void SkipWitnessData(Stream stream, ulong inputCount)
        {
            for (ulong i = 0; i < inputCount; ++i)
            {
                var itemCount = ReadCompactSize(stream);
                for (ulong j = 0; j < itemCount; ++j)
                {
                    var itemSize = ReadCompactSize(stream);
                    SkipBytes(stream, (int)itemSize);
                }
            }
        }

        private static Output AnalyzeOutput(Stream stream)
        {
            // skipping Amount
            SkipBytes(stream, 8);

            var scriptPubKeySize = ReadCompactSize(stream);

            Span<byte> scriptPubKey = stackalloc byte[(int)scriptPubKeySize];
            stream.ReadExactly(scriptPubKey);

            return new Output
            {
                Script = Convert.ToHexString(scriptPubKey).ToLower(),
                ScriptType = ClassifyScriptPubKey(scriptPubKey)
            };
        }

        private static OutputScriptPubKeyType ClassifyScriptPubKey(ReadOnlySpan<byte> scriptPubKey)
        {
            if (scriptPubKey.Length == 67 &&
                scriptPubKey[0] == (byte)ScriptOpcodes.OP_PUSHBYTES_65 &&
                scriptPubKey[^1] == (byte)ScriptOpcodes.OP_CHECKSIG)
            {
                return OutputScriptPubKeyType.P2PK;
            }

            if (scriptPubKey.Length == 35 &&
                scriptPubKey[0] == (byte)ScriptOpcodes.OP_PUSHBYTES_33 &&
                scriptPubKey[^1] == (byte)ScriptOpcodes.OP_CHECKSIG)
            {
                return OutputScriptPubKeyType.P2PK;
            }

            if (scriptPubKey.Length == 25 &&
                scriptPubKey[0] == (byte)ScriptOpcodes.OP_DUP &&
                scriptPubKey[1] == (byte)ScriptOpcodes.OP_HASH160 &&
                scriptPubKey[2] == (byte)ScriptOpcodes.OP_PUSHBYTES_20 &&
                scriptPubKey[^2] == (byte)ScriptOpcodes.OP_EQUALVERIFY &&
                scriptPubKey[^1] == (byte)ScriptOpcodes.OP_CHECKSIG)
            {
                return OutputScriptPubKeyType.P2PKH;
            }

            if (scriptPubKey.Length == 23 &&
                scriptPubKey[0] == (byte)ScriptOpcodes.OP_HASH160 &&
                scriptPubKey[1] == (byte)ScriptOpcodes.OP_PUSHBYTES_20 &&
                scriptPubKey[^1] == (byte)ScriptOpcodes.OP_EQUAL)
            {
                return OutputScriptPubKeyType.P2SH;
            }

            if (scriptPubKey.Length == 22 &&
                scriptPubKey[0] == (byte)ScriptOpcodes.OP_0 &&
                scriptPubKey[1] == (byte)ScriptOpcodes.OP_PUSHBYTES_20)
            {
                return OutputScriptPubKeyType.P2WPKH;
            }

            if (scriptPubKey.Length == 34 &&
                scriptPubKey[0] == (byte)ScriptOpcodes.OP_0 &&
                scriptPubKey[1] == (byte)ScriptOpcodes.OP_PUSHBYTES_32)
            {
                return OutputScriptPubKeyType.P2WSH;
            }

            if (scriptPubKey.Length == 34 &&
                scriptPubKey[0] == (byte)ScriptOpcodes.OP_1 &&
                scriptPubKey[1] == (byte)ScriptOpcodes.OP_PUSHBYTES_32)
            {
                return OutputScriptPubKeyType.P2TR;
            }

            if (scriptPubKey.Length >= 1 &&
                scriptPubKey[0] == (byte)ScriptOpcodes.OP_RETURN)
            {
                return OutputScriptPubKeyType.OP_RETURN;
            }

            if (IsMultisigScript(scriptPubKey))
            {
                return OutputScriptPubKeyType.Multisig;
            }

            return OutputScriptPubKeyType.Nonstandard;
        }

        private static bool IsMultisigScript(ReadOnlySpan<byte> scriptPubKey)
        {
            if (scriptPubKey.Length < 37)
            {
                return false;
            }

            // OP_N is less than OP_M
            if (scriptPubKey[^2] < scriptPubKey[0])
            {
                return false;
            }

            // OP_M must be between OP_1 and OP_16
            if (scriptPubKey[0] < (byte)ScriptOpcodes.OP_1 || scriptPubKey[0] > (byte)ScriptOpcodes.OP_16)
            {
                return false;
            }

            // OP_N must be between OP_1 and OP_16
            if (scriptPubKey[^2] < (byte)ScriptOpcodes.OP_1 || scriptPubKey[^2] > (byte)ScriptOpcodes.OP_16)
            {
                return false;
            }

            if (scriptPubKey[^1] != (byte)ScriptOpcodes.OP_CHECKMULTISIG)
            {
                return false;
            }

            var pubkeysCount = scriptPubKey[^2] - (byte)ScriptOpcodes.OP_1 + 1;
            int currentPubkeysPos = 1;

            for (int i = 0; i < pubkeysCount; ++i)
            {
                if (scriptPubKey[currentPubkeysPos] != (byte)ScriptOpcodes.OP_PUSHBYTES_33 &&
                    scriptPubKey[currentPubkeysPos] != (byte)ScriptOpcodes.OP_PUSHBYTES_65)
                {
                    return false;
                }

                currentPubkeysPos += scriptPubKey[currentPubkeysPos] - (byte)ScriptOpcodes.OP_1 + 2;
            }

            return true;
        }

        private static void SkipBytes(Stream stream, int count)
        {
            stream.Seek(count, SeekOrigin.Current);
        }

        private static ulong ReadCompactSize(Stream stream)
        {
            int firstByte = stream.ReadByte();

            if (firstByte <= 0xFC)
            {
                return (ulong)firstByte;
            }

            var bytesToRead = firstByte switch
            {
                0xFD => 2,
                0xFE => 4,
                0xFF => 8,
                _ => throw new InvalidOperationException($"Invalid CompactSize format. Unexpected flag: 0x{firstByte:X}")
            };
              
            Span<byte> bytes = stackalloc byte[bytesToRead];
            stream.ReadExactly(bytes);

            return ConvertToUInt64WithPadding(bytes);
        }

        private static ulong ConvertToUInt64WithPadding(Span<byte> data)
        {
            int size = sizeof(ulong);
            if (data.Length > size)
            {
                throw new ArgumentException($"Data length exceeds the size of ulong.", nameof(data));
            }

            Span<byte> paddedBytes = stackalloc byte[size];
            data.CopyTo(paddedBytes[..data.Length]);
            return BitConverter.ToUInt64(paddedBytes);
        }
    }
}
