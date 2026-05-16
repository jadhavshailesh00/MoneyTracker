using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Service.Parsing
{
    public class IciciSavingsParser : StatementParserBase
    {
        private static readonly Regex LinePattern = new Regex(
            @"^(?<date>\d{2}-\d{2}-\d{2,4})\s+(?<desc>.*?)\s+(?<withdrawal>[\d,]+\.\d{2})?\s*(?<deposit>[\d,]+\.\d{2})?\s+(?<balance>[\d,]+\.\d{2})$",
            RegexOptions.Compiled);

        protected override Transaction CreateFromDateLine(string line, Match dateMatch)
        {
            var m = LinePattern.Match(line);
            var tx = new Transaction();
            if (m.Success)
            {
                tx.Date = ParseDate(m.Groups["date"].Value);
                tx.Description = m.Groups["desc"].Value.Trim();
                if (m.Groups["withdrawal"].Success) { tx.Amount = DecimalParse(m.Groups["withdrawal"].Value); tx.IsCredit = false; }
                if (m.Groups["deposit"].Success) { tx.Amount = DecimalParse(m.Groups["deposit"].Value); tx.IsCredit = true; }
                if (m.Groups["balance"].Success) tx.Balance = DecimalParse(m.Groups["balance"].Value);
            }
            else
            {
                tx.Date = ParseDate(dateMatch.Groups["date"].Value);
                tx.Description = line.Substring(dateMatch.Length).Trim();
            }
            return tx;
        }

        protected override void FinalizeAmounts(Transaction tx, string line)
        {
            var m = Regex.Match(line, @"(?<amount>[\d,]+\.\d{2})(?!.*\d)");
            if (m.Success && !tx.Amount.HasValue) tx.Amount = DecimalParse(m.Value);
            var mb = Regex.Match(line, @"(?<bal>[\d,]+\.\d{2})$");
            if (mb.Success) tx.Balance = DecimalParse(mb.Value);
        }
    }
}
