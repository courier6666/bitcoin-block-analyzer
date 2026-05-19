# Лабораторна робота 3 Статистика типів вихідних скриптів у блоці

Пояснення роботи коду для обробки біткойн блоку.

---

## Огляд структур даних

В програму було представлено основні типи даних, які використовуються для обробки та зберігання звіту про біткойн блоки.

BlockReport.cs

```
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
```

Клас ```BlockReport``` містить інформацію про висоту блоку, id та масив транзакцій блоку.

Transaction.cs

```
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

```

Клас ```Transaction.cs``` містить id транзакції та масив категорій ```scriptPubKey```, відповідно на кожен вихід - одна категорія.

OutputScriptPubKeyType.cs

```
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

```
```OutputScriptPubKeyType``` - це допоміжний enum клас, який містить кожну категорію ```scriptPubKey```.

ScriptOpcodes.cs

```
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
        OP_CHECKSIG = 0xac,
        OP_HASH160 = 0xa9,
        OP_RETURN = 0x6a,
        OP_EQUALVERIFY = 0x88,
        OP_EQUAL = 0x87,
        OP_DUP = 0x76,
        OP_0 = 0x00,
        OP_1 = 0x51,
        OP_2 = 0x52,
        OP_CHECKMULTISIG = 0xae,
    }
}
```
```ScriptOpcodes``` - це допоміжний enum клас, який містить слова Script, необхідні для визначення категорії виходу.

---
## Аналізатор блоку
Для обробки та аналізу блоку було реалізовано клас ```ScriptTypeAnalyzer```.
Виконаємо повний покроковий опис класу.

В класі доступно два публічні методу:
- метод фабрика для створення аналізатор.
- метод для виконання аналізу блоку та повернення звіту (інформація) блоку.

### CreateAsync

```
  private readonly byte[] _blockRawData;

  private ScriptTypeAnalyzer(byte[] blockRawData)
  {
      _blockRawData = blockRawData;
  }

  public static async Task<ScriptTypeAnalyzer> CreateAsync(string blockPath)
  {
      
      string hexBlockData = await File.ReadAllTextAsync(blockPath);
      string newLineSuffix = hexBlockData[^2..];
      
      // removing \r symbol if exists, as some hex files may end with \r\n
      if (newLineSuffix == "\r\n")
      {
          hexBlockData = hexBlockData[..^2];
      }

      byte[] blockRawData = Convert.FromHexString(hexBlockData);
      return new ScriptTypeAnalyzer(blockRawData);
  }
```
Отже, метод ```CreateAsync``` приймає шлях до даних блоку, зчитує всі дані у вигляді рядка, потім форматує дані, а саме видаляє службові символи, які можуть бути присутні у файлі. Наступним кроком виконується конвертається з hex рядка в масив байтів, який зберігається в класі.

### Analyze

```
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
```

Метод Analyze спочатку поміщає масив в ```MemoryStream```, для зручнішого зчитування.
Наступним кроком є визначення hash id блоку.
Для визначення hash id використовується метод ```GetBlockHashHex```.
Після визначення hash id, виконується зчитування кількості транзакцій, для кожної транзакції виконується аналіз, і кожен результат записується в масив.
І в кінці отримуємо результат аналізу блоку.

### GetBlockHash
```
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
```

Метод приймає масив байтів, який містить дані про заголовок блоку. На початку методу виконується валідація масиву, щоб переконатись, що розмір масиву - 80 байтів (розмір заголовку).

Далі виконується виконання функції HASH256 (виконання функції SHA256 два рази).
Результатом є hash iud, які знаходяться в обрненому вигляді, тому необхідно виконати функцію ```Reverse()```. Останнім кроком є форматування байтів у шістнадцятковому вигляді.

### ReadCompactSize
```
      // reading transaction count
      var transactionCount = ReadCompactSize(stream);
```

В методу Analyze після визначення hash id блоку, виконується зчитування кількість транзакцій в блоку. Значення зберігається в ```CompactSize```.
Тому для цього було створено окремий метод для зчитування такого типу даних.

#### Метод ReadCompactSize

```
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
```

Спочатку у методу зчитуємо перший байт, який вказує на розмір ```CompactSize```.
Якщо значення байту менше або рівне ```0xFC```, тоді все число зберігається в цьому байті. Інакше ми визначаємо розмір числа відповідно яка показано в switch виразі, та перетворюємо масив байтів у восьми байтне число (```ConvertToUInt64WithPadding```).

### GetBlockHeight та ініцілізація BlockReport

```
  var blockReport = new BlockReport
  {
      Height = (int)GetBlockHeight(stream),
      Hash = blockHash,
      Transactions = new Transaction[transactionCount],
  };
```

Наступним кроком в методі Analyze є визначення висоти блоку та ініцілізація BlockReport.
Ініцілізація заповнює поля BlockReport визначеними hash id блоку та масивом транзакцій з розміром ```transactionCount```, який ми визначили раніше.

Проте визначення висоти блоку є складнішим процесом.
Висота блоку знаходиться в першій транзакції в першому вході в полі ```scriptSig```.

```
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
```

На початку ми пропускаємо версію транзакції, далі ми визначаємо чи це SegWit транзакція та зчитуємо відповідні маркери.

Далі зчитуємо кількість входів. Потім пропускаємо 32 байти (transaction id). Длаі пропускаємо ```scriptSig size```.

Після того, як потік дійшов до ```scriptSig```, ми виконуємо зчитування висоти блоку.
Для цього зчитуємо перший байти, який містить кілкіьсть байтів (розмір) числа.

Потім визначені байти парсяться в ulong число, яке містить висоту блоку.

### Аналіз транзакцій

В методі Analyze присутній цикл, який виконує обробку кожної транзакції.

```
  for (ulong i = 0; i < transactionCount; i++)
  {
      blockReport.Transactions[i] = AnalyzeTransaction(stream);
  }
```
Метод ```AnalyzeTransaction```

```
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
          OutputScripts = new OutputScriptPubKeyType[outputCount],
      };

      for (ulong i = 0; i < outputCount; ++i)
      {
          transaction.OutputScripts[i] = AnalyzeOutput(stream);
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
```
На початку здійснюється обрахування hash id транзакції.

```
      // transaction hash
      var transactionId = GetTransactionHashHex(stream);
```
#### GetTransactionHashHex
```
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
```
Hash id транзакцію вираховується через застосування функції HASH(256) на такі дані транзакції: версія, значення кількості входів, всі входи, значення кількості виходів, всі виходи та locktime.
Важливо враховувати, що деякі транзакції є SegWit, тому деякі дані в таких транзакціях треба пропускати.

Отже, спочатку зчитується версія транзакції (4 байти), після чого вона зберігається в масиві.

Далі ми зчитуємо наступний байт та визначаємо чи ця транзакція є SegWit транзакція.
Наступним кроком є зчитування входів та виходів (перше зчитування для визначення розміру). На початку ми зберігаємо позицію потоку, щоб в кінці після зчитування входів та виходів знати початок звідки знову зчитувати вже ля збережння даних в масиві.

Якщо тразнакція є SegWit, необхідно зчитати та пропустити Witness. Якщо не є, тоді просто продовжуємо зчитування.

І останнім зчитування даних, які необхідні для формування hash id транзакції, є locktime.
Отримані три фргаменти (версія, входи-виходи, locktime) зливаємо в один масив.
Після чого виконуємо хешування HASH256 та обертаємо отриманий хеш.
І в кінці конвретуємо в шістнадцятковий вигляд.

#### Подальше виконання методу AnalyzeTransaction
```
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
          OutputScripts = new OutputScriptPubKeyType[outputCount],
      };

      for (ulong i = 0; i < outputCount; ++i)
      {
          transaction.OutputScripts[i] = AnalyzeOutput(stream);
      }

      // Only skip witness data if this is a SegWit transaction
      if (isSegWit)
      {
          SkipWitnessData(stream, inputCount);
      }

      // skipping lock time
      SkipBytes(stream, 4);

      return transaction;
```

Після визначення хешу транзакції, перейдемо далі.

Отже на початку пропускаємо версію транзакції, далі зчитуємо перший байт та визначаємо чи трнанзакція є ```SegWit``` транзакцією.

Після того зчитаємо розмір входів, після чього зчитуємо та пропускаємо відповідну кількість входів.

Далі зчитуємо кількість входів та ініціалзуємо екземпляр класу ```Transaction``` з обраховним хешом та наперед виділеним масивом виходів.

Далі в циклі для кожного виходу визначаємо його категорію для ```scriptPubKey``` завдяки методу ```AnalyzeOutput```.

#### Метод Analyze Output

```
    private static OutputScriptPubKeyType AnalyzeOutput(Stream stream)
    {
        // skipping Amount
        SkipBytes(stream, 8);

        var scriptPubKeySize = ReadCompactSize(stream);

        Span<byte> scriptPubKey = stackalloc byte[(int)scriptPubKeySize];
        stream.ReadExactly(scriptPubKey);

        return ClassifyScriptPubKey(scriptPubKey);
    }
```

Метод ```AnalyzeOutput``` виконує аналіз типу вихідного скрипта.
Спочатку пропускається значення amount. Далі виконується зчитування розміру ```scriptPubKey```.

Визначивши розмір, зчитуємо відповідну кількість байтів та передаємо ці байти в метод ```ClassifyScriptPubKey```.
##### ClassifyScriptPubKey
```
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

            if (scriptPubKey.Length == 71 &&
                scriptPubKey[0] == (byte)ScriptOpcodes.OP_1 &&
                scriptPubKey[1] == (byte)ScriptOpcodes.OP_PUSHBYTES_33 &&
                scriptPubKey[35] == (byte)ScriptOpcodes.OP_PUSHBYTES_33 &&
                scriptPubKey[^2] == (byte)ScriptOpcodes.OP_2 &&
                scriptPubKey[^1] == (byte)ScriptOpcodes.OP_CHECKMULTISIG)
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
```

 Метод ```ClassifyScriptPubKey``` виконує аналіз байтів виходу, а саме їх розмір та значенню певних байтів кодам opcodes та класифікує вихід відповідно як вказано таблиці.


| Тип         | Правило розпізнавання (неформально)                              |
|-------------|------------------------------------------------------------------|
| P2PK        | =<pubkey> OP_CHECKSIG=, де pubkey має 33 або 65 байтів           |
| P2PKH       | =OP_DUP OP_HASH160 <20 байтів> OP_EQUALVERIFY OP_CHECKSIG=       |
| P2SH        | =OP_HASH160 <20 байтів> OP_EQUAL=                                |
| P2WPKH      | =OP_0 <20 байтів>=                                               |
| P2WSH       | =OP_0 <32 байти>=                                                |
| P2TR        | =OP_1 <32 байти>=                                                |
| Multisig    | =<m> <pubkey>... <n> OP_CHECKMULTISIG= (голий, не обгорнутий)    |
| OP_RETURN   | починається з =OP_RETURN= (=0x6a=)                               |
| Nonstandard | будь-що, що не відповідає переліченому вище                      |

#### Продовження AnalyzeTransaction
Після визначення типу виходу, тип записується в масив виходів для транзакції.
```
  var outputCount = ReadCompactSize(stream);
  var transaction = new Transaction
  {
      TransactionId = transactionId,
      OutputScripts = new OutputScriptPubKeyType[outputCount],
  };

  for (ulong i = 0; i < outputCount; ++i)
  {
      transaction.OutputScripts[i] = AnalyzeOutput(stream);
  }

  // Only skip witness data if this is a SegWit transaction
  if (isSegWit)
  {
      SkipWitnessData(stream, inputCount);
  }

  // skipping lock time
  SkipBytes(stream, 4);

  return transaction;
```

Далі зчитується та пропускається witness (якщо це segwit транзакція) і locktime.

### Кінець методу Analyze

Після аналізу транзакцій, результат записуєтьсяв масив та повертається з методу.

```
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
```

## UI
UI містить команди для зручної роботи з аналізатором:
  /load_block "[path]" : завантаження блоку за шляхом.
  /report: вивдення звіту блоку.
  /get_transaction_by_order_number [number]: виведення іноорфмації про транзакцію за номером порядку в масиві.
  /get_transaction [transaction_id]: виведення іноорфмації про транзакцію за hash id.
  /exit : вихід із застосунку.

```
// 949969
using OutputScriptTypeBlockAnalyzer.Core;
using System.Text.RegularExpressions;

BlockReport? currentReport = default!;
Dictionary<string, Transaction> transactionCache = [];
Dictionary<OutputScriptPubKeyType, (int count, float ratio)> scriptTypeStats = [];

string banner = """
    Commands:
        /load_block "[path]" : Load a block from the specified path and analyze it.
        /report: Displays the analysis report of the currently loaded block.
        /get_transaction_by_order_number [number]: Retrieves details of a transaction based on its order in the block.
        /get_transaction [transaction_id]: Retrieves details of a transaction based on its transaction ID.
        /exit : Exits the application.

    """;

while (true)
{
    Console.WriteLine(banner);
    Console.WriteLine("Current report:");
    if (currentReport is not null)
    {
        Console.WriteLine($"Block Height: {currentReport.Height}");
        Console.WriteLine($"Block Hash: {currentReport.Hash}");
        Console.WriteLine($"Number of Transactions: {currentReport.Transactions.Length}");
    }
    else
    {
        Console.WriteLine("No block loaded.");
    }

    Console.WriteLine("==============================================================================");
    Console.Write("Enter command: ");
    var input = Console.ReadLine() ?? string.Empty;

    if (Regex.IsMatch(input, "^/load_block \"(.+)\"$"))
    {
        var match = Regex.Match(input, "^/load_block \"(.+)\"$");
        if (match.Success)
        {
            string path = match.Groups[1].Value;
            currentReport = (await ScriptTypeAnalyzer.CreateAsync(path)).Analyze();
            foreach (var tran in currentReport.Transactions)
            {
                transactionCache[tran.TransactionId] = tran;
            }

            var dictionary = currentReport.Transactions
                .SelectMany(t => t.OutputScripts)
                .GroupBy(s => s)
                .ToDictionary(g => g.Key, g => g.Count());

            int totalOutputs = currentReport.Transactions.Sum(t => t.OutputScripts.Length);
            scriptTypeStats = dictionary.ToDictionary(kv => kv.Key, kv => (kv.Value, (float)kv.Value / totalOutputs));

            Console.WriteLine("Block loaded.");
        }
    }
    else if (Regex.IsMatch(input, "^/report$"))
    {
        if (currentReport is not null)
        {
            Console.WriteLine("Current report:");
            Console.WriteLine($"Block Height: {currentReport.Height} ({currentReport.Hash})");
            Console.WriteLine($"Transactions: {currentReport.Transactions.Length}");
            Console.WriteLine($"Outputs: {currentReport.Transactions.Sum(t => t.OutputScripts.Length)}");
            Console.WriteLine();

            foreach (var kv in scriptTypeStats.OrderByDescending(kv => kv.Value.count))
            {
                Console.WriteLine($"{kv.Key, -20} {kv.Value.count, -20} {kv.Value.ratio:P2}");
            }
        }
        else
        {
            Console.WriteLine("No block loaded.");
        }
    }
    else if (Regex.IsMatch(input, "^/get_transaction_by_order_number \\d+$"))
    {
        if (currentReport is not null)
        {
            var match = Regex.Match(input, "^/get_transaction_by_order_number (\\d+)$");
            if (match.Success)
            {
                int orderNumber = int.Parse(match.Groups[1].Value);
                var transaction = currentReport.Transactions[orderNumber];
                if (transaction is not null)
                {
                    Console.WriteLine($"Transaction {orderNumber}:");
                    Console.WriteLine($"  ID: {transaction.TransactionId}");
                    Console.WriteLine($"  Outputs: {string.Join(", ", transaction.OutputScripts.Select(s => s.ToString()))}");
                }
                else
                {
                    Console.WriteLine($"Transaction {orderNumber} not found.");
                }
            }
        }
        else
        {
            Console.WriteLine("No block loaded.");
        }
    }
    else if (Regex.IsMatch(input, "^/get_transaction \\S+$"))
    {
        if (currentReport is not null)
        {
            var match = Regex.Match(input, "^/get_transaction (\\S+)$");
            if (match.Success)
            {
                string transactionId = match.Groups[1].Value;
                if (transactionCache.TryGetValue(transactionId, out var transaction))
                {
                    Console.WriteLine($"Transaction {transactionId}:");
                    Console.WriteLine($"  ID: {transaction.TransactionId}");
                    Console.WriteLine($"  Outputs: {string.Join(", ", transaction.OutputScripts.Select(s => s.ToString()))}");
                }
                else
                {
                    Console.WriteLine($"Transaction {transactionId} not found.");
                }
            }
        }
        else
        {
            Console.WriteLine("No block loaded.");
        }
    }
    else if (Regex.IsMatch(input, "^/exit$"))
    {
        Console.WriteLine("Exiting...");
        break;
    }
    else
    {
        Console.WriteLine("Unknown command.");
    }

    Console.WriteLine("Enter any key.");
    Console.ReadKey();
}
```