using System.Collections.Generic;

namespace Service.Parsing
{
    public interface IRowGrouper
    {
        IEnumerable<string> GroupToLines(IEnumerable<CoordinateText> words);
    }
}
