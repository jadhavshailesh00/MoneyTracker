using Budget.Events;
using Microsoft.EntityFrameworkCore;

namespace Budget.Services
{
    public interface INotificationService
    {
        Task SendBudgetExceededAlertAsync(Guid userId, Guid budgetId);
        Task CheckBudgetsAsync(Guid userId);
    }

    public class NotificationService : INotificationService
    {
        private readonly MoneyTrackerDbContext _context;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            MoneyTrackerDbContext context, 
            IEventPublisher eventPublisher,
            ILogger<NotificationService> logger)
        {
            _context = context;
            _eventPublisher = eventPublisher;
            _logger = logger;
        }

        public async Task SendBudgetExceededAlertAsync(Guid userId, Guid budgetId)
        {
            var budget = await _context.Budgets
                .Include(b => b.Category)
                .FirstOrDefaultAsync(b => b.BudgetId == budgetId);

            if (budget == null) return;

            var spent = await _context.Transactions
                .Where(t => t.UserId == userId &&
                           t.TransactionDate >= budget.StartDate &&
                           t.TransactionDate <= budget.EndDate &&
                           t.TransactionType == "Debit")
                .SumAsync(t => t.Amount);

            if (spent > budget.AmountLimit)
            {
                // Publish budget exceeded event
                await _eventPublisher.PublishAsync(new BudgetExceededEvent
                {
                    UserId = userId,
                    BudgetId = budgetId,
                    Category = budget.Category?.Name ?? "Unknown",
                    Limit = budget.AmountLimit,
                    Spent = spent
                });

                _logger.LogWarning("Budget exceeded for user {UserId}: {Category} - Spent {Spent}/{Limit}",
                    userId, budget.Category?.Name, spent, budget.AmountLimit);
            }
        }

        public async Task CheckBudgetsAsync(Guid userId)
        {
            var budgets = await _context.Budgets
                .Where(b => b.UserId == userId && b.EndDate >= DateTime.UtcNow.Date)
                .ToListAsync();

            foreach (var budget in budgets)
            {
                await SendBudgetExceededAlertAsync(userId, budget.BudgetId);
            }
        }
    }
}