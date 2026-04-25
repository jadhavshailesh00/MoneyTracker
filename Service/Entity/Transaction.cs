namespace Budget.Entity
{
    public class Transaction
    {
        public Guid TransactionId { get; set; }
        public Guid AccountId { get; set; }
        public Guid UserId { get; set; }
        public decimal Amount { get; set; }
        public string TransactionType { get; set; } = string.Empty; // Debit / Credit
        public string? Description { get; set; }
        public DateTime TransactionDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public Guid? ImportBatchId { get; set; }

        // Navigation properties
        public virtual Account Account { get; set; } = null!;
        public virtual User User { get; set; } = null!;
        public virtual ImportBatch? ImportBatch { get; set; } = null!;
        public virtual ICollection<TransactionCategory> TransactionCategories { get; set; } = new List<TransactionCategory>();
    }
}