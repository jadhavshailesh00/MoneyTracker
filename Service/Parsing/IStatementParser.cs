using System.Collections.Generic;

namespace Service.Parsing
{
    public interface IStatementParser
    {
        List<Transaction> Parse(IEnumerable<string> lines);
    }
}
