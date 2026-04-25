using Budget.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Budget.Services
{
    public interface IAnalyticsService
    {
        Task<MonthlyReportDto> GetMonthlyReportAsync(Guid userId, int year, int month);
        Task<List<CategoryReportDto>> GetCategoryReportAsync(Guid userId, DateTime startDate, DateTime endDate);
        Task<decimal> GetTotalSpendAsync(Guid userId, DateTime startDate, DateTime endDate);
        Task<Dictionary<string, decimal>> GetCategoryAggregatesAsync(Guid userId, DateTime startDate, DateTime endDate);
    }

    public class AnalyticsService : IAnalyticsService
    {
        private readonly MoneyTrackerDbContext _context;
        private readonly ICacheService _cache;

        public AnalyticsService(MoneyTrackerDbContext context, ICacheService cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<MonthlyReportDto> GetMonthlyReportAsync(Guid userId, int year, int month)
        {
            var cacheKey = $"monthly:{userId}:{year}:{month}";
            var cached = await _cache.GetAsync<MonthlyReportDto>(cacheKey);
            if (cached != null) return cached;

            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var transactions = await _context.Transactions
                .Where(t => t.UserId == userId && 
                           t.TransactionDate >= startDate && 
                           t.TransactionDate <= endDate &&
                           !t.IsDeleted)
                .ToListAsync();

            var report = new MonthlyReportDto
            {
                Year = year,
                Month = month,
                TotalIncome = transactions.Where(t => t.TransactionType == "Credit").Sum(t => t.Amount),
                TotalExpense = transactions.Where(t => t.TransactionType == "Debit").Sum(t => t.Amount),
                CategoryBreakdown = new List<CategorySummaryDto>()
            };

            report.NetSavings = report.TotalIncome - report.TotalExpense;

            // Get category breakdown
            var categoryTotals = await GetCategoryAggregatesAsync(userId, startDate, endDate);
            report.CategoryBreakdown = categoryTotals
                .Select(kv => new CategorySummaryDto { Category = kv.Key, Amount = kv.Value })
                .OrderByDescending(c => c.Amount)
                .ToList();

            await _cache.SetAsync(cacheKey, report, TimeSpan.FromMinutes(15));
            return report;
        }

        public async Task<List<CategoryReportDto>> GetCategoryReportAsync(Guid userId, DateTime startDate, DateTime endDate)
        {
            var transactions = await _context.Transactions
                .Where(t => t.UserId == userId &&
                           t.TransactionDate >= startDate &&
                           t.TransactionDate <= endDate &&
                           t.TransactionType == "Debit" &&
                           !t.IsDeleted)
                .Include(t => t.TransactionCategories)
                .ThenInclude(tc => tc.Category)
                .ToListAsync();

            var categoryGroups = transactions
                .SelectMany(t => t.TransactionCategories.Select(tc => new { 
                    Category = tc.Category?.Name ?? "Other", 
                    t.Amount 
                }))
                .GroupBy(x => x.Category)
                .Select(g => new CategoryReportDto
                {
                    Category = g.Key,
                    TotalAmount = g.Sum(x => x.Amount),
                    TransactionCount = g.Count()
                })
                .ToList();

            var total = categoryGroups.Sum(c => c.TotalAmount);
            foreach (var cat in categoryGroups)
            {
                cat.Percentage = total > 0 ? Math.Round((cat.TotalAmount / total) * 100, 2) : 0;
            }

            return categoryGroups.OrderByDescending(c => c.TotalAmount).ToList();
        }

        public async Task<decimal> GetTotalSpendAsync(Guid userId, DateTime startDate, DateTime endDate)
        {
            return await _context.Transactions
                .Where(t => t.UserId == userId &&
                           t.TransactionDate >= startDate &&
                           t.TransactionDate <= endDate &&
                           t.TransactionType == "Debit" &&
                           !t.IsDeleted)
                .SumAsync(t => t.Amount);
        }

        public async Task<Dictionary<string, decimal>> GetCategoryAggregatesAsync(Guid userId, DateTime startDate, DateTime endDate)
        {
            var transactions = await _context.Transactions
                .Where(t => t.UserId == userId &&
                           t.TransactionDate >= startDate &&
                           t.TransactionDate <= endDate &&
                           t.TransactionType == "Debit" &&
                           !t.IsDeleted)
                .Include(t => t.TransactionCategories)
                .ThenInclude(tc => tc.Category)
                .ToListAsync();

            return transactions
                .SelectMany(t => t.TransactionCategories.Select(tc => new {
                    Category = tc.Category?.Name ?? "Uncategorized",
                    t.Amount
                }))
                .GroupBy(x => x.Category)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        }
    }
}