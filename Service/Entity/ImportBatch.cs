namespace Budget.Entity
{
    public class ImportBatch
    {
        public Guid ImportBatchId { get; set; }
        public Guid UserId { get; set; }
        public string Source { get; set; } = string.Empty; // GPay, PhonePe
        public DateTime ImportedAt { get; set; }
        public int TotalRecords { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}