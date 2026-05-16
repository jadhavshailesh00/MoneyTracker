using System;
using System.Linq;

namespace Service.Parsing
{
    public static class UsageExample
    {
        public static void Demo(string pdfPath, int page = 1)
        {
            IPdfTextProvider provider = new PdfPigTextProvider();
            var words = provider.GetPageWords(pdfPath, page);
            var grouper = new ZPatternRowGrouper(yTolerance: 3.0);
            var lines = grouper.GroupToLines(words).ToList();

            // crude header/footer detection
            var startIndex = 0;
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].IndexOf("Statement of Transactions", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    lines[i].IndexOf("Transaction Details", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    startIndex = i + 1;
                    break;
                }
            }

            var content = lines.Skip(startIndex).TakeWhile(l => !l.Contains("Closing Balance", StringComparison.OrdinalIgnoreCase) && !l.Contains("Total:", StringComparison.OrdinalIgnoreCase));

            IStatementParser parser = new IciciSavingsParser();
            var transactions = parser.Parse(content);

            foreach (var t in transactions)
            {
                Console.WriteLine($"{t.Date:d} | {t.Description} | {t.Amount} | {t.Balance} | Credit:{t.IsCredit}");
            }
        }
    }
}
