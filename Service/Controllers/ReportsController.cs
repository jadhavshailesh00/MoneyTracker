using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Budget.Services;
using Budget.Contracts;

namespace Budget.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly INotificationService _notificationService;

        public ReportsController(
            IAnalyticsService analyticsService,
            INotificationService notificationService)
        {
            _analyticsService = analyticsService;
            _notificationService = notificationService;
        }

        [HttpGet("monthly/{year}/{month}")]
        public async Task<ActionResult<MonthlyReportDto>> GetMonthlyReport(int year, int month)
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

        [HttpGet("summary")]
        public async Task<ActionResult> GetSummary([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            var userId = GetUserId();
            var totalSpend = await _analyticsService.GetTotalSpendAsync(userId, startDate, endDate);
            var categories = await _analyticsService.GetCategoryAggregatesAsync(userId, startDate, endDate);
            
            return Ok(new {
                TotalSpend = totalSpend,
                CategoryBreakdown = categories,
                Period = new { StartDate = startDate, EndDate = endDate }
            });
        }

        [HttpPost("check-budgets")]
        public async Task<ActionResult> CheckBudgets()
        {
            var userId = GetUserId();
            await _notificationService.CheckBudgetsAsync(userId);
            return Ok(new { message = "Budget check completed" });
        }

        private Guid GetUserId()
        {
            var claim = User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/nameidentifier");
            return claim != null ? Guid.Parse(claim.Value) : Guid.Empty;
        }
    }
}