namespace Budget.Entity
{
    public class Account
    {
        public Guid AccountId { get; set; }
        public Guid UserId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty; // Bank, Cash, Wallet, Credit
        public string CurrencyCode { get; set; } = "USD";
        public decimal OpeningBalance { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsArchived { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}