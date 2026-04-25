namespace Budget.Contracts
{
    public class TransactionDto
    {
        public Guid TransactionId { get; set; }
        public Guid UserId { get; set; }
        public DateTime TransactionDate { get; set; }
        public decimal Amount { get; set; }
        public string TransactionType { get; set; } = string.Empty; // Debit/Credit
        public string? Description { get; set; }
        public string Source { get; set; } = string.Empty; // GPay/Bank/Card
        public string? ReferenceId { get; set; }
        public string? Category { get; set; }
    }

    public class CreateTransactionDto
    {
        public DateTime TransactionDate { get; set; }
        public decimal Amount { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Source { get; set; } = string.Empty;
        public string? ReferenceId { get; set; }
    }

    public class UploadResponseDto
    {
        public Guid ImportBatchId { get; set; }
        public int TotalRecords { get; set; }
        public int Processed { get; set; }
        public int Failed { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class MonthlyReportDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal NetSavings { get; set; }
        public List<CategorySummaryDto> CategoryBreakdown { get; set; } = new();
    }

    public class CategoryReportDto
    {
        public string Category { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int TransactionCount { get; set; }
        public decimal Percentage { get; set; }
    }

    public class CategorySummaryDto
    {
        public string Category { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class UserDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class LoginRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public UserDto User { get; set; } = null!;
    }

    public class RegisterRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}