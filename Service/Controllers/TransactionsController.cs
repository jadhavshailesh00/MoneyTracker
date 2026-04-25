using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Budget.Services;
using Budget.Contracts;
using System.IO;

namespace Budget.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TransactionsController : ControllerBase
    {
        private readonly IIngestionService _ingestionService;
        private readonly IAnalyticsService _analyticsService;
        private readonly ICategorizationService _categorizationService;

        public TransactionsController(
            IIngestionService ingestionService,
            IAnalyticsService analyticsService,
            ICategorizationService categorizationService)
        {
            _ingestionService = ingestionService;
            _analyticsService = analyticsService;
            _categorizationService = categorizationService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TransactionDto>>> GetTransactions(
            [FromQuery] DateTime? startDate, 
            [FromQuery] DateTime? endDate)
        {
            var userId = GetUserId();
            var query = _analyticsService.GetType()
                .Assembly.GetType("Budget.Services.AnalyticsService");
            
            // Return empty for now - need to add repository method
            return Ok(new List<TransactionDto>());
        }

        [HttpPost("upload")]
        public async Task<ActionResult<UploadResponseDto>> UploadCsv(
            IFormFile file,
            [FromQuery] string source = "GPay")
        {
            var userId = GetUserId();
            
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File is required" });

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

        [HttpPost("categorize")]
        public async Task<ActionResult> CategorizePending()
        {
            await _categorizationService.CategorizeAllPendingAsync();
            return Ok(new { message = "Categorization completed" });
        }

        [HttpGet("monthly")]
        public async Task<ActionResult<MonthlyReportDto>> GetMonthlyReport(
            [FromQuery] int year, 
            [FromQuery] int month)
        {
            var userId = GetUserId();
            var report = await _analyticsService.GetMonthlyReportAsync(userId, year, month);
            return Ok(report);
        }

        [HttpGet("category")]
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