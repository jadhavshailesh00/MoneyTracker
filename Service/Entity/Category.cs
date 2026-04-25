namespace Budget.Entity
{
    public class Category
    {
        public Guid CategoryId { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // Expense / Income
        public Guid? ParentCategoryId { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual Category? ParentCategory { get; set; }
        public virtual ICollection<Category> SubCategories { get; set; } = new List<Category>();
        public virtual ICollection<TransactionCategory> TransactionCategories { get; set; } = new List<TransactionCategory>();
        public virtual ICollection<Budget> Budgets { get; set; } = new List<Budget>();
    }
}