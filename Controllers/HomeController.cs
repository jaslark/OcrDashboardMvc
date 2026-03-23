using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OcrDashboardMvc.Models;
using OcrDashboardMvc.Services;
using System.Diagnostics;

namespace OcrDashboardMvc.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IDashboardService _dashboardService;

    public HomeController(ILogger<HomeController> logger, IDashboardService dashboardService)
    {
        _logger = logger;
        _dashboardService = dashboardService;
    }

    [Route("dashboard")]
    public async Task<IActionResult> Index(FilterModel filters)
    {
        try
        {
            // Khởi tạo filters nếu null
            filters ??= new FilterModel();

            // Gọi một lần duy nhất để lấy tất cả dữ liệu dashboard
            var dashboardData = await _dashboardService.GetAllDashboardDataAsync(filters);

            var viewModel = new DashboardViewModel
            {
                Filters = filters,
                Stats = dashboardData.Stats,
                ScatterData = dashboardData.ScatterData,
                HeatmapData = dashboardData.HeatmapData,
                WorstAccuracyFiles = dashboardData.WorstAccuracyFiles,
                SlowestFiles = dashboardData.SlowestFiles,
                LicenseData = dashboardData.LicenseData,
                PerformanceTrendsData = dashboardData.PerformanceTrendsData,
                TemplateAnalysisData = dashboardData.TemplateAnalysisData
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi DASHBOARD: {Message}", ex.Message);

            // Fallback to empty data in case of error
            var viewModel = new DashboardViewModel
            {
                Filters = filters ?? new FilterModel(),
                Stats = new OcrOverviewStats(),
                ScatterData = new List<ScatterData>(),
                HeatmapData = new HeatmapData
                {
                    Periods = new List<string>(),
                    Templates = new List<string>(),
                    Accuracy = new List<List<double>>(),
                    ProcessingTime = new List<List<double>>()
                },
                WorstAccuracyFiles = new List<FileRecord>(),
                SlowestFiles = new List<FileRecord>(),
                LicenseData = new LicenseData
                {
                    Used = 0,
                    Total = 0,
                    PercentUsed = 0,
                    MonthlyUsage = new List<MonthlyLicenseUsage>()
                },
                PerformanceTrendsData = new PerformanceTrendsData
                {
                    MonthlyTrends = new List<MonthlyPerformanceTrend>()
                },
                TemplateAnalysisData = new TemplateAnalysisData
                {
                    Templates = new List<TemplateStats>()
                }
            };

            ViewBag.ErrorMessage = $"Có lỗi khi tải dữ liệu: {ex.Message}";
            return View(viewModel);
        }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }    
}

