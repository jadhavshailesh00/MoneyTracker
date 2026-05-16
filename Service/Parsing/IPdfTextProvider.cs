using System.Collections.Generic;

namespace Service.Parsing
{
    public interface IPdfTextProvider
    {
        IEnumerable<CoordinateText> GetPageWords(string pdfPath, int pageNumber);
    }
}
