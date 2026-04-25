using Budget.Events;
using Microsoft.EntityFrameworkCore;

namespace Budget.Services
{
    public interface ICategorizationService
    {
        Task<string> CategorizeTransactionAsync(Guid transactionId);
        Task CategorizeAllPendingAsync();
    }

    public class CategorizationService : ICategorizationService
    {
        private readonly MoneyTrackerDbContext _context;
        private readonly IEventPublisher _eventPublisher;

        // Rule-based categorization patterns
        private static readonly Dictionary<string, List<string>> CategoryPatterns = new()
        {
            ["Grocery"] = new() { "grocery", "supermart", "big basket", "zepto", "blinkit", "swiggy instamart" },
            ["Fuel"] = new() { "petrol", "fuel", "bunk", "ioc", "hpcl", "bpcl", "shell" },
            ["Food"] = new() { "restaurant", "food", "zomato", "swiggy", "domino", "mcdonalds", "kfc", "pizza" },
            ["Shopping"] = new() { "amazon", "flipkart", "myntra", "meesho", "shopify" },
            ["Bills"] = new() { "electricity", "water", "gas", "bill", "recharge", "mobile", "jio", "airtel" },
            ["Transport"] = new() { "uber", "ola", "metro", "railway", "irctc", "flight" },
            ["Entertainment"] = new() { "netflix", "hotstar", "prime video", "movie", "theatre", "bookmyshow" },
            ["Salary"] = new() { "salary", "income", "credit", "refund" },
            ["Investment"] = new() { "sip", "mutual fund", "stock", "fd", "nps" }
        };

        public CategorizationService(MoneyTrackerDbContext context, IEventPublisher eventPublisher)
        {
            _context = context;
            _eventPublisher = eventPublisher;
        }

        public async Task<string> CategorizeTransactionAsync(Guid transactionId)
        {
            var transaction = await _context.Transactions.FindAsync(transactionId);
            if (transaction == null) return "Other";

            var category = DetermineCategory(transaction.Description ?? "", transaction.TransactionType);
            
            // Link to category if exists
            var categoryEntity = await _context.Categories
                .FirstOrDefaultAsync(c => c.UserId == transaction.UserId && c.Name == category);

            if (categoryEntity != null)
            {
                var txCategory = new Entity.TransactionCategory
                {
                    TransactionId = transactionId,
                    CategoryId = categoryEntity.CategoryId
                };
                _context.TransactionCategories.Add(txCategory);
                await _context.SaveChangesAsync();
            }

            // Publish categorized event
            await _eventPublisher.PublishAsync(new TransactionCategorizedEvent
            {
                TransactionId = transactionId,
                UserId = transaction.UserId,
                Category = category
            });

            return category;
        }

        public async Task CategorizeAllPendingAsync()
        {
            var pendingTransactions = await _context.Transactions
                .Where(t => !_context.TransactionCategories.Any(tc => tc.TransactionId == t.TransactionId))
                .ToListAsync();

            foreach (var transaction in pendingTransactions)
            {
                await CategorizeTransactionAsync(transaction.TransactionId);
            }
        }

        private string DetermineCategory(string description, string transactionType)
        {
            var lowerDesc = description.ToLower();

            foreach (var (category, patterns) in CategoryPatterns)
            {
                if (patterns.Any(p => lowerDesc.Contains(p)))
                {
                    // Income transactions default to Salary/Investment
                    if (transactionType == "Credit" && (category == "Grocery" || category == "Food" || category == "Shopping"))
                    {
                        return "Other Income";
                    }
                    return category;
                }
            }

            return "Other";
        }
    }
}