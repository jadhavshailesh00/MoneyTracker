using System;
using Service.Parsing;
using System.Linq;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: PdfTest <pdf-path> [pageNumber]");
            return 2;
        }

        var path = args[0];
        var pageNumber = 1;
        if (args.Length > 1 && int.TryParse(args[1], out var p)) pageNumber = p;

        try
        {
            IPdfTextProvider provider = new PdfPigTextProvider();
            var words = provider.GetPageWords(path, pageNumber).ToList();
            var grouper = new ZPatternRowGrouper(yTolerance: 3.0);
            var lines = grouper.GroupToLines(words).ToList();

            Console.WriteLine($"Grouped lines (page {pageNumber}) - {lines.Count} lines:");
            for (int i = 0; i < lines.Count; i++)
            {
                Console.WriteLine($"{i+1:000}: {lines[i]}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error reading PDF: " + ex.Message);
            return 3;
        }
    }
}
