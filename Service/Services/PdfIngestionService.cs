using System.Text.RegularExpressions;
using Budget.Events;
using Budget.Contracts;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Budget.Services
{
    public interface IPdfIngestionService
    {
        Task<UploadResponseDto> ProcessBankStatementPdfAsync(Guid userId, Stream stream, string bankName);
    }

    public class PdfIngestionService : IPdfIngestionService
    {
        private readonly MoneyTrackerDbContext _context;
        private readonly IEventPublisher _eventPublisher;

        // Bank-specific patterns for PDF parsing
        private static readonly Dictionary<string, BankParserConfig> BankConfigs = new()
        {
            ["SBI"] = new BankParserConfig
            {
                DatePattern = @"\d{2}-\d{2}-\d{2}",
                // Match: Date | Transaction | Reference | Ref.No. | Credit | Debit | Balance
                TransactionPattern = @"^(\d{2}-\d{2}-\d{2})\s+(.+?)\s+(\S+)\s+(\S+)\s+([\d,]+\.?\d*)\s+([\d,]+\.?\d*)\s+([\d,]+\.?\d*)",
                CreditColumn = 4, // 0-based index
                DebitColumn = 5,
                BalanceColumn = 6,
                SkipPatterns = new[] { "balance", "available", "total", "page", "statement", "branch", "ifsc" }
            },
            ["HDFC"] = new BankParserConfig
            {
                DatePatterns = new[] { @"\d{2}-\d{2}-\d{4}", @"\d{2}/\d{2}/\d{4}" },
                AmountPatterns = new[] { @"\d{1,3}(,\d{3})*(\.\d{2})?" },
                DescriptionStartLine = 1,
                CreditKeywords = new[] { "CR", "CREDIT", "NEFT", "IMPS", "UPI" },
                DebitKeywords = new[] { "DR", "DEBIT", "DEBIT", "CHQ", "ATM" },
                SkipLines = new[] { "balance", "available", "total", "page" }
            },
            ["ICICI"] = new BankParserConfig
            {
                DatePatterns = new[] { @"\d{2}/\d{2}/\d{4}", @"\d{2}-\d{2}-\d{4}" },
                AmountPatterns = new[] { @"\d{1,3}(,\d{3})*(\.\d{2})?" },
                DescriptionStartLine = 1,
                CreditKeywords = new[] { "CR", "CREDIT", "NEFT", "RTGS" },
                DebitKeywords = new[] { "DR", "DEBIT", "CHQ", "ATM" },
                SkipLines = new[] { "balance", "available", "total", "page" }
            },
            ["DEFAULT"] = new BankParserConfig
            {
                DatePatterns = new[] { @"\d{2}[/-]\d{2}[/-]\d{4}" },
                AmountPatterns = new[] { @"\d{1,3}(,\d{3})*(\.\d{2})?" },
                DescriptionStartLine = 1,
                CreditKeywords = new[] { "CR", "CREDIT", "DEPOSIT" },
                DebitKeywords = new[] { "DR", "DEBIT", "WITHDRAWAL" },
                SkipLines = new[] { "balance", "total", "page" }
            }
        };

        public PdfIngestionService(MoneyTrackerDbContext context, IEventPublisher eventPublisher)
        {
            _context = context;
            _eventPublisher = eventPublisher;
        }

        public async Task<UploadResponseDto> ProcessBankStatementPdfAsync(Guid userId, Stream stream, string bankName)
        {
            var response = new UploadResponseDto();
            var importBatch = new Entity.ImportBatch
            {
                ImportBatchId = Guid.NewGuid(),
                UserId = userId,
                Source = $"PDF-{bankName}",
                ImportedAt = DateTime.UtcNow,
                TotalRecords = 0
            };

            var config = BankConfigs.ContainsKey(bankName.ToUpper()) 
                ? BankConfigs[bankName.ToUpper()] 
                : BankConfigs["DEFAULT"];

            try
            {
                using var document = PdfDocument.Open(stream);
                var fullText = new System.Text.StringBuilder();

                foreach (var page in document.GetPages())
                {
                    fullText.AppendLine(page.Text);
                }

                List<ParsedTransaction> transactions;

                // Use bank-specific parser
                if (bankName.ToUpper() == "SBI")
                {
                    transactions = ParseSbiStatement(fullText.ToString(), config);
                }
                else
                {
                    transactions = ParsePdfText(fullText.ToString(), config);
                }
                
                foreach (var tx in transactions)
                {
                    try
                    {
                        var transaction = new Entity.Transaction
                        {
                            TransactionId = Guid.NewGuid(),
                            UserId = userId,
                            AccountId = await GetOrCreateDefaultAccountAsync(userId),
                            Amount = tx.Amount,
                            TransactionType = tx.IsCredit ? "Credit" : "Debit",
                            Description = tx.Description,
                            TransactionDate = tx.Date,
                            CreatedAt = DateTime.UtcNow,
                            ImportBatchId = importBatch.ImportBatchId
                        };

                        _context.Transactions.Add(transaction);
                        importBatch.TotalRecords++;

                        // Publish event
                        await _eventPublisher.PublishAsync(new TransactionCreatedEvent
                        {
                            TransactionId = transaction.TransactionId,
                            UserId = userId,
                            ImportBatchId = importBatch.ImportBatchId,
                            TransactionDate = transaction.TransactionDate,
                            Amount = transaction.Amount,
                            TransactionType = transaction.TransactionType,
                            Description = transaction.Description,
                            Source = $"PDF-{bankName}"
                        });

                        response.Processed++;
                    }
                    catch (Exception ex)
                    {
                        response.Failed++;
                        response.Errors.Add($"Transaction error: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                response.Errors.Add($"PDF parsing error: {ex.Message}");
            }

            _context.ImportBatches.Add(importBatch);
            await _context.SaveChangesAsync();
            response.ImportBatchId = importBatch.ImportBatchId;
            response.TotalRecords = importBatch.TotalRecords;

            return response;
        }

        /// <summary>
        /// Parse SBI statement with exact format:
        /// Date | Transaction | Reference | Ref.No. | Credit | Debit | Balance
        /// </summary>
        private List<ParsedTransaction> ParseSbiStatement(string text, BankParserConfig config)
        {
            var transactions = new List<ParsedTransaction>();
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var cleanLine = line.Trim();
                if (string.IsNullOrWhiteSpace(cleanLine)) continue;

                // Skip header/footer lines
                if (config.SkipPatterns.Any(s => cleanLine.ToLower().Contains(s))) continue;

                // SBI Format: Date | Transaction | Reference | Ref.No. | Credit | Debit | Balance
                // Example: 01-03-26 UPI/DR/606092391512/VINOD KU/YESB/q827107277/UPI - 0 10.00 47310.14
                var match = Regex.Match(cleanLine, config.TransactionPattern);
                
                if (match.Success)
                {
                    try
                    {
                        // Parse date (DD-MM-YY format)
                        var dateStr = match.Groups[1].Value;
                        if (!DateTime.TryParseExact(dateStr, "dd-MM-yy", null, 
                            System.Globalization.DateTimeStyles.None, out var date))
                        {
                            continue;
                        }

                        // Parse description
                        var description = match.Groups[2].Value.Trim();

                        // Parse credit amount
                        var creditStr = match.Groups[config.CreditColumn].Value.Trim().Replace(",", "");
                        var debitStr = match.Groups[config.DebitColumn].Value.Trim().Replace(",", "");

                        decimal credit = 0, debit = 0;
                        decimal.TryParse(creditStr, out credit);
                        decimal.TryParse(debitStr, out debit);

                        if (credit == 0 && debit == 0) continue;

                        var amount = credit > 0 ? credit : debit;
                        var isCredit = credit > 0;

                        transactions.Add(new ParsedTransaction
                        {
                            Date = date,
                            Amount = Math.Abs(amount),
                            Description = description.Length > 255 ? description.Substring(0, 255) : description,
                            IsCredit = isCredit
                        });
                    }
                    catch
                    {
                        // Skip malformed lines
                        continue;
                    }
                }
            }

            return transactions.OrderBy(t => t.Date).ToList();
        }

        private List<ParsedTransaction> ParsePdfText(string text, BankParserConfig config)
        {
            var transactions = new List<ParsedTransaction>();
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var cleanLine = line.Trim();
                if (string.IsNullOrWhiteSpace(cleanLine)) continue;

                // Skip header/footer lines
                if (config.SkipLines.Any(s => cleanLine.ToLower().Contains(s))) continue;

                // Try to extract date
                DateTime? date = null;
                foreach (var datePattern in config.DatePatterns)
                {
                    var match = Regex.Match(cleanLine, datePattern);
                    if (match.Success)
                    {
                        if (DateTime.TryParse(match.Value, out var parsedDate))
                        {
                            date = parsedDate;
                            break;
                        }
                    }
                }

                if (date == null) continue;

                // Try to extract amounts
                var amounts = ExtractAmounts(cleanLine, config.AmountPatterns);
                if (amounts.Count == 0) continue;

                // Determine credit/debit
                var isCredit = config.CreditKeywords.Any(k => cleanLine.ToUpper().Contains(k));
                var isDebit = config.DebitKeywords.Any(k => cleanLine.ToUpper().Contains(k));

                if (!isCredit && !isDebit && amounts.Count == 1)
                {
                    // Default to debit for single amounts
                    isDebit = true;
                }

                if (amounts.Count > 0)
                {
                    var amount = amounts.First();
                    var description = cleanLine;

                    // Clean up description - remove amounts and dates
                    foreach (var datePattern in config.DatePatterns)
                    {
                        description = Regex.Replace(description, datePattern, "");
                    }
                    foreach (var amt in amounts)
                    {
                        description = description.Replace(amt.ToString("N2"), "");
                    }
                    description = Regex.Replace(description, @"[CR|DR]\s*", "").Trim();

                    transactions.Add(new ParsedTransaction
                    {
                        Date = date.Value,
                        Amount = Math.Abs(amount),
                        Description = description.Length > 255 ? description.Substring(0, 255) : description,
                        IsCredit = isCredit
                    });
                }
            }

            return transactions.OrderBy(t => t.Date).ToList();
        }

        private List<decimal> ExtractAmounts(string line, string[] patterns)
        {
            var amounts = new List<decimal>();
            
            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(line, pattern);
                foreach (Match match in matches)
                {
                    var amountStr = match.Value.Replace(",", "");
                    if (decimal.TryParse(amountStr, out var amount) && amount > 0)
                    {
                        amounts.Add(amount);
                    }
                }
            }

            return amounts.Distinct().ToList();
        }

        private async Task<Guid> GetOrCreateDefaultAccountAsync(Guid userId)
        {
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.UserId == userId && a.AccountType == "Bank");
            
            if (account == null)
            {
                account = new Entity.Account
                {
                    AccountId = Guid.NewGuid(),
                    UserId = userId,
                    AccountName = "Default Bank Account",
                    AccountType = "Bank",
                    CurrencyCode = "INR",
                    OpeningBalance = 0,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();
            }

            return account.AccountId;
        }
    }

    public class BankParserConfig
    {
        public string[] DatePatterns { get; set; } = Array.Empty<string>();
        public string DatePattern { get; set; } = string.Empty;
        public string TransactionPattern { get; set; } = string.Empty;
        public string[] AmountPatterns { get; set; } = Array.Empty<string>();
        public int DescriptionStartLine { get; set; }
        public int CreditColumn { get; set; }
        public int DebitColumn { get; set; }
        public int BalanceColumn { get; set; }
        public string[] CreditKeywords { get; set; } = Array.Empty<string>();
        public string[] DebitKeywords { get; set; } = Array.Empty<string>();
        public string[] SkipLines { get; set; } = Array.Empty<string>();
        public string[] SkipPatterns { get; set; } = Array.Empty<string>();
    }

    public class ParsedTransaction
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsCredit { get; set; }
    }
}