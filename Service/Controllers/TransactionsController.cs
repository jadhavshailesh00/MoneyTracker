using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Budget.Services;
using Budget.Contracts;
using System.IO;

namespace Budget.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
   // [Authorize]
    public class TransactionsController : ControllerBase
    {
        private readonly IIngestionService _ingestionService;
        private readonly IPdfIngestionService _pdfIngestionService;
        private readonly IAnalyticsService _analyticsService;
        private readonly ICategorizationService _categorizationService;

        public TransactionsController(
            IIngestionService ingestionService,
            IPdfIngestionService pdfIngestionService,
            IAnalyticsService analyticsService,
            ICategorizationService categorizationService)
        {
            _ingestionService = ingestionService;
            _pdfIngestionService = pdfIngestionService;
            _analyticsService = analyticsService;
            _categorizationService = categorizationService;
        }

        /// <summary>
        /// Upload CSV file (GPay, Bank statement)
        /// </summary>
        /// <remarks>
        /// Upload a CSV file containing transaction data.
        /// Supported sources: GPay, Bank
        /// 
        /// Example CSV format for GPay:
        /// ```csv
        /// Date,Description,TransactionId,Amount,Type
        /// 2024-01-15,SWIGGY,UTR123456,150,Debit
        /// 2024-01-16,SALARY,UTR789012,50000,Credit
        /// ```
        /// 
        /// Example CSV format for Bank:
        /// ```csv
        /// Date,Narration,Debit,Credit
        /// 15-01-2024,UBER TRIP,150.00,0.00
        /// 16-01-2024,SALARY CREDIT,0.00,50000.00
        /// ```
        /// </remarks>
        [HttpPost("upload")]
        [ProducesResponseType(typeof(UploadResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<UploadResponseDto>> UploadCsv(
            IFormFile file,
            [FromQuery] string source = "GPay")
        {
            var userId = GetUserId();
            
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File is required" });

            var allowedExtensions = new[] { ".csv", ".txt" };
            var extension = System.IO.Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { message = "Only CSV files are allowed" });

            var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), file.FileName);
            using (var stream = new System.IO.FileStream(tempPath, System.IO.FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            try
            {
                var result = await _ingestionService.ProcessCsvUploadAsync(userId, tempPath, source);
                return Ok(result);
            }
            finally
            {
                if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath);
            }
        }

        /// <summary>
        /// Upload PDF bank statement
        /// </summary>
        /// <remarks>
        /// Upload a PDF bank statement for processing.
        /// Supported banks: SBI, HDFC, ICICI, or DEFAULT for other banks
        /// 
        /// The PDF will be parsed to extract transaction details including:
        /// - Transaction date
        /// - Description/Narration
        /// - Credit/Debit amounts
        /// 
        /// Note: PDF must be a text-based PDF (not scanned image)
        /// </remarks>
        [HttpPost("upload-pdf")]
        [ProducesResponseType(typeof(UploadResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<UploadResponseDto>> UploadPdf(
            IFormFile file,
            [FromQuery] string bankName = "DEFAULT")
        {
            var userId = GetUserId();
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File is required" });

            var extension = System.IO.Path.GetExtension(file.FileName).ToLower();
            if (extension != ".pdf")
                return BadRequest(new { message = "Only PDF files are allowed" });

            using var stream = file.OpenReadStream();
            var result = await _pdfIngestionService.ProcessBankStatementPdfAsync(userId, stream, bankName);
            return Ok(result);
        }

        /// <summary>
        /// Get supported bank list for PDF parsing
        /// </summary>
        [HttpGet("supported-banks")]
        public ActionResult<IEnumerable<string>> GetSupportedBanks()
        {
            return Ok(new[] { "SBI", "HDFC", "ICICI", "DEFAULT" });
        }

        /// <summary>
        /// Categorize all pending transactions
        /// </summary>
        /// <remarks>
        /// Runs rule-based categorization on all transactions that haven't been categorized yet.
        /// Categories: Grocery, Fuel, Food, Shopping, Bills, Transport, Entertainment, Salary, Investment, Other
        /// </remarks>
        [HttpPost("categorize")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> CategorizePending()
        {
            await _categorizationService.CategorizeAllPendingAsync();
            return Ok(new { message = "Categorization completed" });
        }

        /// <summary>
        /// Get monthly report
        /// </summary>
        [HttpGet("monthly")]
        [ProducesResponseType(typeof(MonthlyReportDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<MonthlyReportDto>> GetMonthlyReport(
            [FromQuery] int year, 
            [FromQuery] int month)
        {
            var userId = GetUserId();
            var report = await _analyticsService.GetMonthlyReportAsync(userId, year, month);
            return Ok(report);
        }

        /// <summary>
        /// Get category-wise report
        /// </summary>
        [HttpGet("category")]
        [ProducesResponseType(typeof(List<CategoryReportDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IEnumerable<CategoryReportDto>>> GetCategoryReport(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            var userId = GetUserId();
            var report = await _analyticsService.GetCategoryReportAsync(userId, startDate, endDate);
            return Ok(report);
        }

        private Guid GetUserId()
        {
            var claim = User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/nameidentifier");
            return claim != null ? Guid.Parse(claim.Value) : Guid.Empty;
        }
    }
}