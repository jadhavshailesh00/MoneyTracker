namespace Budget.Entity
{
    public class Budget
    {
        public Guid BudgetId { get; set; }
        public Guid UserId { get; set; }
        public Guid CategoryId { get; set; }
        public decimal AmountLimit { get; set; }
        public string Period { get; set; } = string.Empty; // Monthly, Weekly
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual Category Category { get; set; } = null!;
    }
}