using System.Globalization;
using Budget.Events;
using Budget.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Budget.Services
{
    public interface IIngestionService
    {
        Task<UploadResponseDto> ProcessCsvUploadAsync(Guid userId, string filePath, string source);
        Task<UploadResponseDto> ProcessGPayCsvAsync(Guid userId, Stream stream);
        Task<UploadResponseDto> ProcessBankCsvAsync(Guid userId, Stream stream);
    }

    public class IngestionService : IIngestionService
    {
        private readonly MoneyTrackerDbContext _context;
        private readonly IEventPublisher _eventPublisher;

        public IngestionService(MoneyTrackerDbContext context, IEventPublisher eventPublisher)
        {
            _context = context;
            _eventPublisher = eventPublisher;
        }

        public async Task<UploadResponseDto> ProcessCsvUploadAsync(Guid userId, string filePath, string source)
        {
            var lines = await File.ReadAllLinesAsync(filePath);
            return source.ToUpper() switch
            {
                "GPAY" => await ProcessGPayCsvAsync(userId, File.OpenRead(filePath)),
                "BANK" => await ProcessBankCsvAsync(userId, File.OpenRead(filePath)),
                _ => new UploadResponseDto { Errors = new List<string> { "Unknown source" } }
            };
        }

        public async Task<UploadResponseDto> ProcessGPayCsvAsync(Guid userId, Stream stream)
        {
            var response = new UploadResponseDto();
            var importBatch = new Entity.ImportBatch
            {
                ImportBatchId = Guid.NewGuid(),
                UserId = userId,
                Source = "GPay",
                ImportedAt = DateTime.UtcNow,
                TotalRecords = 0
            };

            using var reader = new StreamReader(stream);
            var lines = new List<string>();
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (line != null) lines.Add(line);
            }

            // Skip header, process data rows
            for (int i = 1; i < lines.Count; i++)
            {
                try
                {
                    var parts = ParseGPayLine(lines[i]);
                    if (parts == null) continue;

                    var transaction = new Entity.Transaction
                    {
                        TransactionId = Guid.NewGuid(),
                        UserId = userId,
                        AccountId = await GetOrCreateDefaultAccountAsync(userId),
                        Amount = Math.Abs(parts.Value.amount),
                        TransactionType = parts.Value.isCredit ? "Credit" : "Debit",
                        Description = parts.Value.description,
                        TransactionDate = parts.Value.date,
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
                        Source = "GPay"
                    });

                    response.Processed++;
                }
                catch (Exception ex)
                {
                    response.Failed++;
                    response.Errors.Add($"Line {i}: {ex.Message}");
                }
            }

            _context.ImportBatches.Add(importBatch);
            await _context.SaveChangesAsync();
            response.ImportBatchId = importBatch.ImportBatchId;
            response.TotalRecords = importBatch.TotalRecords;

            return response;
        }

        public async Task<UploadResponseDto> ProcessBankCsvAsync(Guid userId, Stream stream)
        {
            var response = new UploadResponseDto();
            var importBatch = new Entity.ImportBatch
            {
                ImportBatchId = Guid.NewGuid(),
                UserId = userId,
                Source = "Bank",
                ImportedAt = DateTime.UtcNow,
                TotalRecords = 0
            };

            using var reader = new StreamReader(stream);
            var lines = new List<string>();
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (line != null) lines.Add(line);
            }

            for (int i = 1; i < lines.Count; i++)
            {
                try
                {
                    var parts = ParseBankLine(lines[i]);
                    if (parts == null) continue;

                    var transaction = new Entity.Transaction
                    {
                        TransactionId = Guid.NewGuid(),
                        UserId = userId,
                        AccountId = await GetOrCreateDefaultAccountAsync(userId),
                        Amount = Math.Abs(parts.Value.amount),
                        TransactionType = parts.Value.isCredit ? "Credit" : "Debit",
                        Description = parts.Value.description,
                        TransactionDate = parts.Value.date,
                        CreatedAt = DateTime.UtcNow,
                        ImportBatchId = importBatch.ImportBatchId
                    };

                    _context.Transactions.Add(transaction);
                    importBatch.TotalRecords++;

                    await _eventPublisher.PublishAsync(new TransactionCreatedEvent
                    {
                        TransactionId = transaction.TransactionId,
                        UserId = userId,
                        ImportBatchId = importBatch.ImportBatchId,
                        TransactionDate = transaction.TransactionDate,
                        Amount = transaction.Amount,
                        TransactionType = transaction.TransactionType,
                        Description = transaction.Description,
                        Source = "Bank"
                    });

                    response.Processed++;
                }
                catch (Exception ex)
                {
                    response.Failed++;
                    response.Errors.Add($"Line {i}: {ex.Message}");
                }
            }

            _context.ImportBatches.Add(importBatch);
            await _context.SaveChangesAsync();
            response.ImportBatchId = importBatch.ImportBatchId;
            response.TotalRecords = importBatch.TotalRecords;

            return response;
        }

        private (DateTime date, decimal amount, string description, bool isCredit)? ParseGPayLine(string line)
        {
            // GPay CSV format: Date,Description,TransactionId,Amount,Credit/Debit
            var parts = line.Split(',');
            if (parts.Length < 5) return null;

            if (DateTime.TryParse(parts[0], out var date) &&
                decimal.TryParse(parts[3], out var amount))
            {
                return (date, amount, parts[1], parts[4].Trim() == "Credit");
            }
            return null;
        }

        private (DateTime date, decimal amount, string description, bool isCredit)? ParseBankLine(string line)
        {
            // Bank CSV format: Date,Narration,Debit,Credit
            var parts = line.Split(',');
            if (parts.Length < 4) return null;

            if (DateTime.TryParse(parts[0], out var date))
            {
                decimal debit = 0, credit = 0;
                decimal.TryParse(parts[2], out debit);
                decimal.TryParse(parts[3], out credit);
                var amount = debit > 0 ? debit : credit;
                return (date, amount, parts[1], credit > 0);
            }
            return null;
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
}