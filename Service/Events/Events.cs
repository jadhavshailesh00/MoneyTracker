namespace Budget.Events
{
    public class TransactionCreatedEvent
    {
        public Guid TransactionId { get; set; }
        public Guid UserId { get; set; }
        public Guid ImportBatchId { get; set; }
        public DateTime TransactionDate { get; set; }
        public decimal Amount { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Source { get; set; } = string.Empty;
        public string? ReferenceId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class TransactionCategorizedEvent
    {
        public Guid TransactionId { get; set; }
        public Guid UserId { get; set; }
        public string Category { get; set; } = string.Empty;
        public DateTime CategorizedAt { get; set; } = DateTime.UtcNow;
    }

    public class BudgetExceededEvent
    {
        public Guid UserId { get; set; }
        public Guid BudgetId { get; set; }
        public string Category { get; set; } = string.Empty;
        public decimal Limit { get; set; }
        public decimal Spent { get; set; }
        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
    }
}