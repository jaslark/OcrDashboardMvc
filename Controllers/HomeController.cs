using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OcrDashboardMvc.Models;
using OcrDashboardMvc.Services;
using System.Diagnostics;

namespace OcrDashboardMvc.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IDashboardService _dashboardService;
    private readonly IConfiguration _configuration;

    public HomeController(ILogger<HomeController> logger, IDashboardService dashboardService, IConfiguration configuration)
    {
        _logger = logger;
        _dashboardService = dashboardService;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index(FilterModel filters)
    {
        try
        {
            // Khởi tạo filters nếu null
            filters ??= new FilterModel();

            // Lấy Total License Pages từ appsettings
            var totalLicensePages = _configuration.GetValue<int>("LicenseSettings:TotalLicensePages", 10000);

            // Gọi một lần duy nhất để lấy tất cả dữ liệu dashboard
            var dashboardData = await _dashboardService.GetAllDashboardDataAsync(filters, totalLicensePages);

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
                    Total = 10000,
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

