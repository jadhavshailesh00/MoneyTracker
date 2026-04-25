namespace Budget.Entity
{
    public class TransactionCategory
    {
        public Guid TransactionId { get; set; }
        public Guid CategoryId { get; set; }

        // Navigation properties
        public virtual Transaction Transaction { get; set; } = null!;
        public virtual Category Category { get; set; } = null!;
    }
}