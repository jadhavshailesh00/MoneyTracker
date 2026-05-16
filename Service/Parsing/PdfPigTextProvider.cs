using System.Collections.Generic;
using UglyToad.PdfPig;

namespace Service.Parsing
{
    public class PdfPigTextProvider : IPdfTextProvider
    {
        public IEnumerable<CoordinateText> GetPageWords(string pdfPath, int pageNumber)
        {
            using var doc = PdfDocument.Open(pdfPath);
            var page = doc.GetPage(pageNumber);
            foreach (var word in page.GetWords())
            {
                yield return new CoordinateText
                {
                    Text = word.Text,
                    X = word.BoundingBox.Left,
                    Y = word.BoundingBox.Top,
                    Width = word.BoundingBox.Width,
                    Height = word.BoundingBox.Height
                };
            }
        }
    }
}
