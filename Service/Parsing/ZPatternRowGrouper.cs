using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Service.Parsing
{
    public class ZPatternRowGrouper : IRowGrouper
    {
        private readonly double _yTolerance;

        public ZPatternRowGrouper(double yTolerance = 3.0)
        {
            _yTolerance = yTolerance;
        }

        public IEnumerable<string> GroupToLines(IEnumerable<CoordinateText> words)
        {
            var list = words.OrderByDescending(w => w.Y).ThenBy(w => w.X).ToList();
            var rows = new List<List<CoordinateText>>();
            foreach (var w in list)
            {
                var row = rows.LastOrDefault(r => Math.Abs(r.Average(x => x.Y) - w.Y) <= _yTolerance);
                if (row == null)
                {
                    rows.Add(new List<CoordinateText> { w });
                }
                else
                {
                    row.Add(w);
                }
            }

            foreach (var r in rows)
            {
                var line = string.Join(" ", r.OrderBy(x => x.X).Select(x => x.Text));
                yield return Regex.Replace(line, "\\s+", " ").Trim();
            }
        }
    }
}
