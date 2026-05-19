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