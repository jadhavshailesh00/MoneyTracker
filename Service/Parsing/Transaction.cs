using System;

namespace Service.Parsing
{
    public class Transaction
    {
        public DateTime? Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal? Amount { get; set; }
        public decimal? Balance { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public bool IsCredit { get; set; }
    }
}
