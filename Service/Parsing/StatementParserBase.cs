using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Service.Parsing
{
    public abstract class StatementParserBase : IStatementParser
    {
        protected readonly Regex DateStartRegex = new Regex(@"^(?<date>\d{2}[-/]\d{2}[-/]\d{2,4})", RegexOptions.Compiled);
        protected readonly Regex AmountRegex = new Regex(@"(?<amount>[\d,]+\.\d{2})$", RegexOptions.Compiled);

        public List<Transaction> Parse(IEnumerable<string> lines)
        {
            var results = new List<Transaction>();
            Transaction current = null;
            foreach (var line in lines)
            {
                var mDate = DateStartRegex.Match(line);
                if (mDate.Success)
                {
                    if (current != null) results.Add(current);
                    current = CreateFromDateLine(line, mDate);
                }
                else if (current != null)
                {
                    current.Description += " " + line.Trim();
                    var mAmount = AmountRegex.Match(line);
                    if (mAmount.Success) FinalizeAmounts(current, line);
                }
            }
            if (current != null) results.Add(current);
            return results;
        }

        protected abstract Transaction CreateFromDateLine(string line, Match dateMatch);
        protected abstract void FinalizeAmounts(Transaction tx, string line);

        protected DateTime? ParseDate(string s)
        {
            var formats = new[] { "dd-MM-yyyy", "dd-MM-yy", "dd/MM/yyyy", "dd/MM/yy" };
            if (DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) return dt;
            if (DateTime.TryParse(s, out dt)) return dt;
            return null;
        }

        protected decimal? DecimalParse(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var cleaned = s.Replace(",", string.Empty).Trim();
            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return d;
            return null;
        }
    }
}
