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

        public Output[] Outputs { get; set; } = default!;
    }
}

```

Клас ```Transaction.cs``` містить id транзакції та масив виходів ```Output```.

Output.cs
```
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
```

Клас ```Output.cs``` містить дані про вихід, а саме скрипт та категорію скрипта.

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
```

Після визначення хешу транзакції, перейдемо далі.

Отже на початку пропускаємо версію транзакції, далі зчитуємо перший байт та визначаємо чи трнанзакція є ```SegWit``` транзакцією.

Після того зчитаємо розмір входів, після чього зчитуємо та пропускаємо відповідну кількість входів.

Далі зчитуємо кількість виходів та ініціалізуємо екземпляр класу ```Transaction``` з обраховним хешом та наперед виділеним масивом виходів.

Далі в циклі для кожного виходу визначаємо його категорію для ```scriptPubKey``` та самі дані скрипта завдяки методу ```AnalyzeOutput```.

#### Метод Analyze Output

```
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
```

Метод ```AnalyzeOutput``` виконує аналіз типу вихідного скрипта.
Спочатку пропускається значення amount. Далі виконується зчитування розміру ```scriptPubKey```.

Визначивши розмір, зчитуємо відповідну кількість байтів, зберігаємо ці дані в екземплярі класу ```Output``` та передаємо ці байти в метод ```ClassifyScriptPubKey``` для визначення типу скрипта.
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

            if (isMultisigScript(scriptPubKey))
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
                if (currentPubkeysPos >= scriptPubKey.Length - 2)
                {
                    return false;
                }

                if (scriptPubKey[currentPubkeysPos] != (byte)ScriptOpcodes.OP_PUSHBYTES_33 &&
                    scriptPubKey[currentPubkeysPos] != (byte)ScriptOpcodes.OP_PUSHBYTES_65)
                {
                    return false;
                }

                currentPubkeysPos += scriptPubKey[currentPubkeysPos] + 1;
            }

            if (currentPubkeysPos != scriptPubKey.Length - 2)
            {
                return false;
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
```

Далі зчитується та пропускається witness (якщо це segwit транзакція) і locktime.

### Кінець методу Analyze

Після аналізу транзакцій, результат записується в масив та повертається з методу.

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
```
  /load_block "[path]" : завантаження блоку за шляхом.
  /report: вивдення звіту блоку.
  /get_transaction_by_order_number [number]: виведення іноорфмації про транзакцію за номером порядку в масиві.
  /get_transaction [transaction_id]: виведення інформації про транзакцію за hash id.
  /exit : вихід із застосунку.
```
```
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
## Результат виконання програми та її перевірка
Запустимо програму та перевіримо її виконання.
Завантажимо блок:

<p align="center">
  <img src="https://github.com/courier6666/bitcoin-block-analyzer/blob/main/Photos/load_block.jpg?raw=true" width="900"/>
</p>

На рисунку можна побачити завантаження блоку та поточний блок.
Висота блоку - 949847

Хеш блоку- 00000000000000000001a4a9dff4a036126d5de07a508cbb65e304a3a9bb2919

Кількість транзакцій в блоці - 5001.

Звіримо з getblock командою
<p align="center">
  <img src="https://github.com/courier6666/bitcoin-block-analyzer/blob/main/Photos/getblock.jpg?raw=true" width="900"/>
</p>

Отже, можна побачити, що висота кількість транзакцій та висота блоку сходиться, отже програма правильно зчитує та визначає дані про блок в сирих даних блоку.

Тепер перевіримо чи правильно програма зчитує транзакції та їхні дані.
<p align="center">
  <img src="https://github.com/courier6666/bitcoin-block-analyzer/blob/main/Photos/first_transaction.jpg?raw=true" width="900"/>
</p>

Можна побачити, що перша транзакція має хеш:
a4f5993c5bec7f34ec3ddd8ae2147e759b2e8e1012c260a975d6edb5642c55e9

Звіримо це за командою getblock та подивимось на першу траназкцію.
<p align="center">
  <img src="https://github.com/courier6666/bitcoin-block-analyzer/blob/main/Photos/first_transaction_bitcoin-cli.jpg?raw=true" width="900"/>
</p>

В самому низу картинки можна побачити, що хеш першої транзакції співпадає, отже програма правильно зчитує транзакції.

Тепер виведимо звіт статистики по типам виходів в транзакціях блоку.
<p align="center">
  <img src="https://github.com/courier6666/bitcoin-block-analyzer/blob/main/Photos/report.jpg?raw=true" width="900"/>
</p>

В блоці висотою 949847 найбільше зустрічається виходів P2WPKH та OP_RETURN.

Тепер перевіримо чи правильно визначаються типи виходів в транзакціях.
Транзакція 0:
<p align="center">
  <img src="https://github.com/courier6666/bitcoin-block-analyzer/blob/main/Photos/transaction_0.jpg?raw=true" width="900"/>
</p>

Дані транзакції 0 в bitcoin-cli:
```
{
  "hash": "a4f5993c5bec7f34ec3ddd8ae2147e759b2e8e1012c260a975d6edb5642c55e9",
  "ver": 1,
  "vin_sz": 1,
  "vout_sz": 7,
  "size": 471,
  "weight": 1776,
  "fee": 0,
  "relayed_by": "0.0.0.0",
  "lock_time": 0,
  "tx_index": 8209664841268666,
  "double_spend": false,
  "time": 1779044593,
  "block_index": 949847,
  "block_height": 949847,
  "inputs": [
    {
      "sequence": 4294967295,
      "witness": "01200000000000000000000000000000000000000000000000000000000000000000",
      "script": "03577e0e194d696e656420627920416e74506f6f6c206a0108029c1a5c4efabe6d6d9f8c47ec7dc2021f11efbe7d5e5f76235ad85ffcb327f202146e0217fd3b2b471000000000000000000035677a13000000000000",
      "index": 0,
      "prev_out": {
        "type": 0,
        "spent": true,
        "value": 0,
        "spending_outpoints": [
          {
            "tx_index": 8209664841268666,
            "n": 0
          }
        ],
        "n": 4294967295,
        "tx_index": 0,
        "script": ""
      }
    }
  ],
  "out": [
    {
      "type": 0,
      "spent": false,
      "value": 546,
      "spending_outpoints": [],
      "n": 0,
      "tx_index": 8209664841268666,
      "script": "a91442402a28dd61f2718a4b27ae72a4791d5bbdade787",
      "addr": "37jKPSmbEGwgfacCr2nayn1wTaqMAbA94Z"
    },
    {
      "type": 0,
      "spent": true,
      "value": 313222576,
      "spending_outpoints": [
        {
          "tx_index": 6042102497052715,
          "n": 10
        }
      ],
      "n": 1,
      "tx_index": 8209664841268666,
      "script": "a9145249bdf2c131d43995cff42e8feee293f79297a887",
      "addr": "39C7fxSzEACPjM78Z7xdPxhf7mKxJwvfMJ"
    },
    {
      "type": 0,
      "spent": false,
      "value": 0,
      "spending_outpoints": [],
      "n": 2,
      "tx_index": 8209664841268666,
      "script": "6a24aa21a9ed53747803e89f0b4eff805598e52a78799c3af73057a48dcc28c409ec0fa14fbe"
    },
    {
      "type": 0,
      "spent": false,
      "value": 0,
      "spending_outpoints": [],
      "n": 3,
      "tx_index": 8209664841268666,
      "script": "6a2d434f524501a37cf4faa0758b26dca666f3e36d42fa15cc01064e3ecda72cb7961caa4b541b1e322bcfe0b5a030"
    },
    {
      "type": 0,
      "spent": false,
      "value": 0,
      "spending_outpoints": [],
      "n": 4,
      "tx_index": 8209664841268666,
      "script": "6a12455853415401000d130f0e0e0b041f120013"
    },
    {
      "type": 0,
      "spent": false,
      "value": 0,
      "spending_outpoints": [],
      "n": 5,
      "tx_index": 8209664841268666,
      "script": "6a277379730a8d35f8788aa5e02266f4cbb775403749e0115ec4b5e3a86c65c502c17083ebea2f2200"
    },
    {
      "type": 0,
      "spent": false,
      "value": 0,
      "spending_outpoints": [],
      "n": 6,
      "tx_index": 8209664841268666,
      "script": "6a2952534b424c4f434b3a8953b2762271940ce8c042ee81b723d5832a8e4a848cb50272994113008702bf"
    }
  ]
}
```

Отже можна побачити, що дані виходів скриптів співпадають.
Наприклад вихід 7 має такі дані 
```6a2952534b424c4f434b3a8953b2762271940ce8c042ee81b723d5832a8e4a848cb50272994113008702bf```
Дані починаються з байту ```6a```, що відповідає опкоду ```OP_RETURN```.
Отже, програма правильно визначає типи скриптів виходів.

Порівняємо для 4 інших транзакцій.
Транзакція 1:
<p align="center">
  <img src="https://github.com/courier6666/bitcoin-block-analyzer/blob/main/Photos/transaction_1.jpg?raw=true" width="900"/>
</p>

```
{
  "hash": "64ddb028f4e14d0633186cb9e6dea7a503bfa0b12f57c667526b1c8a95628e56",
  "ver": 1,
  "vin_sz": 1,
  "vout_sz": 2,
  "size": 222,
  "weight": 561,
  "fee": 2260,
  "relayed_by": "0.0.0.0",
  "lock_time": 0,
  "tx_index": 3045425257989005,
  "double_spend": false,
  "time": 1779044554,
  "block_index": 949847,
  "block_height": 949847,
  "inputs": [
    {
      "sequence": 4294960000,
      "witness": "0247304402203d17da6319de3648fa70e0e3a245ef2960be4edd596a0f3bba2dcac2a783ea0102202932b2974e79c6ab71e59153cf02e73fdd9e3822005edcfc01212b35dbda63c8012102234192bb7808bc273b19870facd6547512f3864fb408c5952da7ce9e94dee65a",
      "script": "",
      "index": 0,
      "prev_out": {
        "type": 0,
        "spent": true,
        "value": 7092553,
        "spending_outpoints": [
          {
            "tx_index": 3045425257989005,
            "n": 0
          }
        ],
        "n": 1,
        "tx_index": 174610348856950,
        "script": "00140fa58d97016f49bbb82f40cfbf86b399eaac447a",
        "addr": "bc1qp7jcm9cpdaymhwp0gr8mlp4nn842c3r6nmtaq5"
      }
    }
  ],
  "out": [
    {
      "type": 0,
      "spent": false,
      "value": 1278072,
      "spending_outpoints": [],
      "n": 0,
      "tx_index": 3045425257989005,
      "script": "0014c0e20eba509ff1b1590ab3d5c4bc1093458609a6",
      "addr": "bc1qcr3qawjsnlcmzkg2k02uf0qsjdzcvzdxfsrrk2"
    },
    {
      "type": 0,
      "spent": true,
      "value": 5812221,
      "spending_outpoints": [
        {
          "tx_index": 2511118228423104,
          "n": 0
        }
      ],
      "n": 1,
      "tx_index": 3045425257989005,
      "script": "00140fa58d97016f49bbb82f40cfbf86b399eaac447a",
      "addr": "bc1qp7jcm9cpdaymhwp0gr8mlp4nn842c3r6nmtaq5"
    }
  ]
}
```

Транзакція 10:
<p align="center">
  <img src="https://github.com/courier6666/bitcoin-block-analyzer/blob/main/Photos/transaction_10.jpg?raw=true" width="900"/>
</p>

```
{
  "hash": "8cca0af348c7e953cbf888d5a70cdcd6a82dbb07e63bbe130cead54a052ff5be",
  "ver": 1,
  "vin_sz": 1,
  "vout_sz": 1,
  "size": 195,
  "weight": 450,
  "fee": 587,
  "relayed_by": "0.0.0.0",
  "lock_time": 0,
  "tx_index": 6718728484510397,
  "double_spend": false,
  "time": 1779044542,
  "block_index": 949847,
  "block_height": 949847,
  "inputs": [
    {
      "sequence": 4294967295,
      "witness": "02483045022100e1c23cb5d1bdff7094554c4d29844368d61f1a9f30416ba4669ae5a5edb167fc0220666a72358d25beae93aa3adc238b3a0efbcaf4cb4996ac94bd1b996989d4775c01210282292b2048aebcc1cb4ceb948019e8a6782adcaa879429122066d16f3a703766",
      "script": "",
      "index": 0,
      "prev_out": {
        "type": 0,
        "spent": true,
        "value": 430587,
        "spending_outpoints": [
          {
            "tx_index": 6718728484510397,
            "n": 0
          }
        ],
        "n": 0,
        "tx_index": 4665674116523577,
        "script": "0014a0d3d36f40073e0fa4c670a2533673e6c74a6a1a",
        "addr": "bc1q5rfaxm6qqulqlfxxwz39xdnnumr556s6rp2yuz"
      }
    }
  ],
  "out": [
    {
      "type": 0,
      "spent": true,
      "value": 430000,
      "spending_outpoints": [
        {
          "tx_index": 8050009356057482,
          "n": 306
        }
      ],
      "n": 0,
      "tx_index": 6718728484510397,
      "script": "76a914941108e5f6ed85706419681d3085a3af40ca2f6988ac",
      "addr": "1EVuQzEHAZ8856GHgv182jytpW1VLiXPB1"
    }
  ]
}
```

Транзакція 2001:
<p align="center">
  <img src="https://github.com/courier6666/bitcoin-block-analyzer/blob/main/Photos/transaction_2001.jpg?raw=true" width="900"/>
</p>

```
{
  "hash": "29bb856aaa5ff58bcfb766506f6a2390fd1aeee1a1934eb53fcd81a234047875",
  "ver": 2,
  "vin_sz": 1,
  "vout_sz": 2,
  "size": 222,
  "weight": 558,
  "fee": 43,
  "relayed_by": "0.0.0.0",
  "lock_time": 0,
  "tx_index": 4133066466676793,
  "double_spend": false,
  "time": 1779043606,
  "block_index": 949847,
  "block_height": 949847,
  "inputs": [
    {
      "sequence": 4294967293,
      "witness": "02483045022100dc2438bdc821170c47d22c4ae9f5b339c3512e2a2778f397ba0ef619a4d69cb202205042e05b824703240c7e76473d7397ec67f3724cfd12e18f68bcad45c670dace01210360aa5eafe246010c379c88a36a23d9f09e3fe7463efb5653716e81712533c246",
      "script": "",
      "index": 0,
      "prev_out": {
        "type": 0,
        "spent": true,
        "value": 79282,
        "spending_outpoints": [
          {
            "tx_index": 4133066466676793,
            "n": 0
          }
        ],
        "n": 0,
        "tx_index": 8470132828567990,
        "script": "001401c902d5e3fa01e19e4e2baaefcdafef75a52b9f",
        "addr": "bc1qq8ys940rlgq7r8jw9w4wlnd0aa6622ulvukj4j"
      }
    }
  ],
  "out": [
    {
      "type": 0,
      "spent": true,
      "value": 79239,
      "spending_outpoints": [
        {
          "tx_index": 6502070012057292,
          "n": 0
        }
      ],
      "n": 0,
      "tx_index": 4133066466676793,
      "script": "001401c902d5e3fa01e19e4e2baaefcdafef75a52b9f",
      "addr": "bc1qq8ys940rlgq7r8jw9w4wlnd0aa6622ulvukj4j"
    },
    {
      "type": 0,
      "spent": false,
      "value": 0,
      "spending_outpoints": [],
      "n": 1,
      "tx_index": 4133066466676793,
      "script": "6a5d1214011400ff7f818cec82d08bc0a88281d215"
    }
  ]
}
```

Транзакція 3000:
<p align="center">
  <img src="https://github.com/courier6666/bitcoin-block-analyzer/blob/main/Photos/transaction_3000.jpg?raw=true" width="900"/>
</p>

```
{
  "hash": "6b91e51fa86c19e13de770cf16f92df227ccd2e6a69a76fb3b1d55efed5911c0",
  "ver": 2,
  "vin_sz": 1,
  "vout_sz": 2,
  "size": 221,
  "weight": 557,
  "fee": 51,
  "relayed_by": "0.0.0.0",
  "lock_time": 0,
  "tx_index": 6757784183761571,
  "double_spend": false,
  "time": 1779043897,
  "block_index": 949847,
  "block_height": 949847,
  "inputs": [
    {
      "sequence": 4294967293,
      "witness": "0247304402204bc25c11660383b47c3bff61e4adf58da5d2a14810190c4715c0cd2bd557e40f02204e86801a85a213b841910568cde81f98541655ad61f877d7313054447603207d0121031a1b8f51f411b3ff567f72e0c94d4276e47bc66d7c1d1aa8deb8f989b7595fdc",
      "script": "",
      "index": 0,
      "prev_out": {
        "type": 0,
        "spent": true,
        "value": 1099,
        "spending_outpoints": [
          {
            "tx_index": 6757784183761571,
            "n": 0
          }
        ],
        "n": 0,
        "tx_index": 6763714655264151,
        "script": "0014c1a40f46ac79d47408a9ec0e63865e1cd5dea64c",
        "addr": "bc1qcxjq734v0828gz9fas8x8pj7rn2aafjvlamxf6"
      }
    }
  ],
  "out": [
    {
      "type": 0,
      "spent": true,
      "value": 1048,
      "spending_outpoints": [
        {
          "tx_index": 7090608457183466,
          "n": 0
        }
      ],
      "n": 0,
      "tx_index": 6757784183761571,
      "script": "0014c1a40f46ac79d47408a9ec0e63865e1cd5dea64c",
      "addr": "bc1qcxjq734v0828gz9fas8x8pj7rn2aafjvlamxf6"
    },
    {
      "type": 0,
      "spent": false,
      "value": 0,
      "spending_outpoints": [],
      "n": 1,
      "tx_index": 6757784183761571,
      "script": "6a5d1214011400ff7f818cec82d08bc0a88281d215"
    }
  ]
}
```

Отже, на прикладах даних транзакцій можна побачити, що програма привильно зчитує транзакції та її виходи, і правильно визначає категорії скриптів у виходах.