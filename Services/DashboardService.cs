using OcrDashboardMvc.Database;
using OcrDashboardMvc.Models;
using System.Globalization;

namespace OcrDashboardMvc.Services
{
    public interface IDashboardService
    {
        Task<DashboardDataBundle> GetAllDashboardDataAsync(FilterModel filters);
        Task<OcrOverviewStats> GetOcrOverviewStatsAsync(FilterModel filters);
        Task<List<ScatterData>> GetScatterDataAsync(FilterModel filters);
        Task<HeatmapData> GetHeatmapDataAsync(FilterModel filters);
        Task<List<FileRecord>> GetWorstAccuracyFilesAsync(FilterModel filters, int top = 10);
        Task<List<FileRecord>> GetSlowestFilesAsync(FilterModel filters, int top = 10);
        Task<LicenseData> GetLicenseDataAsync(FilterModel filters);
        Task<PerformanceTrendsData> GetPerformanceTrendsDataAsync(FilterModel filters);
        Task<TemplateAnalysisData> GetTemplateAnalysisDataAsync(FilterModel filters);
    }

    // New class to bundle all dashboard data
    public class DashboardDataBundle
    {
        public OcrOverviewStats Stats { get; set; }
        public List<ScatterData> ScatterData { get; set; }
        public HeatmapData HeatmapData { get; set; }
        public List<FileRecord> WorstAccuracyFiles { get; set; }
        public List<FileRecord> SlowestFiles { get; set; }
        public LicenseData LicenseData { get; set; }
        public PerformanceTrendsData PerformanceTrendsData { get; set; }
        public TemplateAnalysisData TemplateAnalysisData { get; set; }
    }

    public class DashboardService : IDashboardService
    {
        private readonly ISqlApiProxyDatabase _database;
        private readonly ILogger<DashboardService> _logger;
        private const string TableName = "ocr_clos.sftpocrfile"; //public.ocr_requests

        public DashboardService(ISqlApiProxyDatabase database, ILogger<DashboardService> logger)
        {
            _database = database;
            _logger = logger;
        }

        /// <summary>
        /// Optimized method to get all dashboard data with minimal database round trips
        /// </summary>
        public async Task<DashboardDataBundle> GetAllDashboardDataAsync(FilterModel filters)
        {
            try
            {
                var (fromDate, toDate) = ParseDateRange(filters);

                // CỘNG NGÀY TRỰC TIẾP Ở C# để DB không phải tính toán (SARGable)
                var toDateNextDay = toDate.AddDays(1);

                var (whereClause, parameters) = BuildWhereClauseAndParameters(filters, fromDate, toDateNextDay);

                // Khởi tạo tất cả các Task cùng lúc (KHÔNG AWAIT ở đây)
                var statsTask = GetOcrOverviewStatsInternalAsync(fromDate, toDateNextDay, whereClause, parameters);
                var scatterTask = GetScatterDataInternalAsync(fromDate, toDateNextDay, whereClause, parameters);
                var worstAccuracyTask = GetWorstAccuracyFilesInternalAsync(fromDate, toDateNextDay, whereClause, parameters, 10);
                var slowestTask = GetSlowestFilesInternalAsync(fromDate, toDateNextDay, whereClause, parameters, 10);
                var licenseTask = GetLicenseDataInternalAsync(fromDate, toDateNextDay, whereClause, parameters);
                var heatmapTask = GetHeatmapDataInternalAsync(fromDate, toDateNextDay);
                var performanceTask = GetPerformanceTrendsDataInternalAsync(fromDate, toDateNextDay, whereClause, parameters);
                var templateTask = GetTemplateAnalysisDataInternalAsync(fromDate, toDateNextDay);

                // Chờ TẤT CẢ cùng hoàn thành (Thời gian response = thời gian của Task chạy lâu nhất)
                await Task.WhenAll(
                    statsTask, scatterTask, worstAccuracyTask, slowestTask,
                    licenseTask, heatmapTask, performanceTask, templateTask
                );

                return new DashboardDataBundle
                {
                    Stats = statsTask.Result,
                    ScatterData = scatterTask.Result,
                    WorstAccuracyFiles = worstAccuracyTask.Result,
                    SlowestFiles = slowestTask.Result,
                    LicenseData = licenseTask.Result,
                    HeatmapData = heatmapTask.Result,
                    PerformanceTrendsData = performanceTask.Result,
                    TemplateAnalysisData = templateTask.Result
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi SERVICE - GetAllDashboardDataAsync: {Message}", ex.Message);
                throw;
            }
        }

        private (string Clause, List<object> Parameters) BuildWhereClauseAndParameters(
    FilterModel filters, DateTime fromDate, DateTime toDateNextDay)
        {
            var conditions = new List<string>();
            var parameters = new List<object> { fromDate, toDateNextDay };
            // @0 = fromDate, @1 = toDateNextDay

            // Xử lý Template
            if (!string.IsNullOrEmpty(filters.Template) && filters.Template != "all")
            {
                if (filters.Template == "Unknown")
                {
                    conditions.Add("circular IS NULL AND typeocr IS NULL");
                }
                else if (filters.Template == "TT133")
                {
                    conditions.Add($"circular = @{parameters.Count}");
                    parameters.Add(filters.Template);
                }
                else
                {
                    conditions.Add($"typeocr = @{parameters.Count}");
                    parameters.Add(filters.Template);
                }
            }

            // Xử lý Status
            if (!string.IsNullOrEmpty(filters.Status) && filters.Status != "all")
            {
                if (filters.Status == "manual")
                {
                    conditions.Add("circular IS NOT NULL");
                }
                else
                {
                    conditions.Add($"statusocr = @{parameters.Count}");
                    parameters.Add(filters.Status == "success" ? (int)OcrFileStatus.Completed : 3);
                }
            }

            string whereClause = conditions.Count > 0
                ? "AND " + string.Join(" AND ", conditions)
                : string.Empty;

            return (whereClause, parameters);
        }

        // Internal optimized methods with pre-parsed parameters
        private async Task<OcrOverviewStats> GetOcrOverviewStatsInternalAsync(
               DateTime fromDate, DateTime toDate, string whereClause, List<object> parameters)
        {
            var sql = "";
            try
            {
                var completedStatus = (int)OcrFileStatus.Completed;
                var failedStatuses = string.Join(",", GetFailedStatusValues());

                sql = $@"
                        SELECT 
                  COUNT(*) as total_reports,
                  SUM(CASE WHEN statusocr = {completedStatus} THEN 1 ELSE 0 END) as ocr_success,
                       SUM(CASE WHEN statusocr IN ({failedStatuses}) THEN 1 ELSE 0 END) as ocr_failed,
                  SUM(CASE WHEN circular IS NOT NULL THEN 1 ELSE 0 END) as ocr_manual,
                        COALESCE(SUM(pagecount), 0) as total_pages,
                   AVG(CASE 
                                WHEN statusocr = {completedStatus} AND pagecount > 0 AND timeocr IS NOT NULL AND NULLIF(timeocr::text, '') IS NOT NULL 
                    THEN EXTRACT(EPOCH FROM timeocr::interval) / pagecount
                      ELSE NULL
                 END) as avg_speed,
                   AVG(CASE WHEN statusocr = {completedStatus} THEN COALESCE(accuracyrate, 0) ELSE NULL END) as avg_accuracy
                      FROM {TableName}
                        WHERE uploadtime >= @0
                            AND uploadtime < @1 {whereClause}";

                var result = await _database.FetchAsync<dynamic>(sql, parameters.ToArray());
                var stats = result.FirstOrDefault();

                if (stats == null)
                {
                    return new OcrOverviewStats();
                }

                var totalReports = (long)stats.total_reports;
                var ocrSuccess = (long)stats.ocr_success;
                var successRate = totalReports > 0 ? Math.Round((double)ocrSuccess / totalReports * 100, 1) : 0;
                var avgSpeed = stats.avg_speed != null ? Math.Round((double)stats.avg_speed, 1) : 0;
                var avgAccuracy = stats.avg_accuracy != null ? Math.Round((double)stats.avg_accuracy, 1) : 0;

                return new OcrOverviewStats
                {
                    TotalReports = (int)totalReports,
                    OcrSuccess = (int)ocrSuccess,
                    OcrFailed = (int)(long)stats.ocr_failed,
                    OcrManual = (int)(long)stats.ocr_manual,
                    TotalPages = (int)(long)stats.total_pages,
                    SuccessRate = successRate,
                    AccuracyRate = avgAccuracy,
                    AvgProcessingSpeed = avgSpeed
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi Stats - SQL: {SQL}", sql);
                throw;
            }
        }

        private async Task<List<ScatterData>> GetScatterDataInternalAsync(
    DateTime fromDate,
    DateTime toDate,
    string whereClause,
    List<object> parameters)
        {
            var sql = "";

            try
            {
                sql = $@"
        SELECT 
            id AS id,
            COALESCE(circular, typeocr, 'Unknown') AS template,
            CASE 
                WHEN pagecount > 0 AND timeocr IS NOT NULL
                THEN ROUND((EXTRACT(EPOCH FROM timeocr) / pagecount)::numeric, 1)
                ELSE 0
            END AS time,
            ROUND(COALESCE(accuracyrate, 0)::numeric, 1) AS accuracy
        FROM public.ocr_requests
        WHERE uploadtime BETWEEN @0 AND @1
        AND pagecount > 0
        {whereClause}
        ORDER BY uploadtime DESC";

                var args = new List<object> { fromDate, toDate };
                args.AddRange(parameters);

                _logger.LogInformation(sql);

                foreach (var p in args)
                {
                    _logger.LogInformation(p.ToString());
                }

                var result = await _database.FetchAsync<ScatterData>(sql, args.ToArray());
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi ScatterData - SQL: {SQL}", sql);
                return new List<ScatterData>();
            }
        }

        private async Task<List<FileRecord>> GetWorstAccuracyFilesInternalAsync(
    DateTime fromDate, DateTime toDate, string whereClause, List<object> parameters, int top)
        {
            var sql = "";
            try
            {
                var paramsCopy = new List<object>(parameters) { top };

                sql = $@"
                    SELECT 
                        filename AS RecordCode,
                        COALESCE(circular, typeocr, 'Unknown') AS Template,
                        TO_CHAR(created, 'YYYY-MM-DD') AS OcrDate,
                    ROUND(CAST(COALESCE(accuracyrate, 0) AS numeric), 1) AS Accuracy,
                    CASE 
                                WHEN pagecount > 0 AND timeocr IS NOT NULL AND NULLIF(timeocr::text, '') IS NOT NULL
                    THEN ROUND(CAST(EXTRACT(EPOCH FROM timeocr::interval) / pagecount AS numeric), 1)
                            ELSE 0
                    END AS ProcessingTime,
                    pagecount AS Pages
                    FROM {TableName}
                    WHERE uploadtime >= @0
                                AND uploadtime < @1
                            AND accuracyrate IS NOT NULL
                        {whereClause}
                        ORDER BY accuracyrate ASC
                LIMIT @{parameters.Count}";

                return await _database.FetchAsync<FileRecord>(sql, paramsCopy.ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi WorstAccuracyFiles - SQL: {SQL}", sql);
                return new List<FileRecord>();
            }
        }

        private async Task<List<FileRecord>> GetSlowestFilesInternalAsync(
    DateTime fromDate, DateTime toDate, string whereClause, List<object> parameters, int top)
        {
            var sql = "";
            try
            {
                var paramsCopy = new List<object>(parameters) { top };

                sql = $@"
                  SELECT 
                     filename AS RecordCode,
                      COALESCE(circular, typeocr, 'Unknown') AS Template,
                  TO_CHAR(created, 'YYYY-MM-DD') AS OcrDate,
               ROUND(CAST(COALESCE(accuracyrate, 0) AS numeric), 1) AS Accuracy,
                CASE 
                    WHEN pagecount > 0 AND timeocr IS NOT NULL AND NULLIF(timeocr::text, '') IS NOT NULL 
                  THEN ROUND(CAST(EXTRACT(EPOCH FROM timeocr::interval) / pagecount AS numeric), 1)
                    ELSE 0
                 END AS ProcessingTime,
                  pagecount AS Pages
                 FROM {TableName}
                  WHERE uploadtime >= @0
                    AND uploadtime < @1
                       AND pagecount > 0
                  AND timeocr IS NOT NULL
                  AND NULLIF(timeocr::text, '') IS NOT NULL
               {whereClause}
              ORDER BY ProcessingTime DESC
                 LIMIT {top}";

                return await _database.FetchAsync<FileRecord>(sql, paramsCopy.ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi SlowestFiles - SQL: {SQL}", sql);
                return new List<FileRecord>();
            }
        }

        private async Task<LicenseData> GetLicenseDataInternalAsync(
              DateTime fromDate, DateTime toDate, string whereClause, List<object> parameters)
        {
            var sql = string.Empty;
            var ledgerSql = @"
                   SELECT 
                     total_purchased,
                     total_used
                   FROM public.licenseledger
                   ORDER BY seq DESC
                   LIMIT 1";

            var ledgerTotalPurchased = 0;
            var ledgerTotalUsed = 0;
            var hasLedgerData = false;

            try
            {
                var ledgerResult = await _database.FetchAsync<dynamic>(ledgerSql);
                var latestLedger = ledgerResult.FirstOrDefault();

                if (latestLedger != null)
                {
                    ledgerTotalPurchased = ConvertToInt(latestLedger.total_purchased);
                    ledgerTotalUsed = ConvertToInt(latestLedger.total_used);
                    hasLedgerData = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi LicenseLedger - SQL: {SQL}", ledgerSql);
            }

            try
            {
                var completedStatus = (int)OcrFileStatus.Completed;

                sql = $@"
                   SELECT 
                 (SELECT COALESCE(SUM(pagecount), 0) FROM {TableName} WHERE statusocr = {completedStatus}) as total_used,
                    TO_CHAR(uploadtime, 'TMMonth') AS month_name,
                         EXTRACT(MONTH FROM uploadtime) AS month_num,
                          EXTRACT(YEAR FROM uploadtime) AS year,
                      COALESCE(SUM(pagecount), 0) AS usage
                            FROM {TableName}
                                  WHERE uploadtime >= @0
                                AND uploadtime < @1
                       AND statusocr = {completedStatus}
                         {whereClause}
                 GROUP BY EXTRACT(YEAR FROM uploadtime), EXTRACT(MONTH FROM uploadtime), TO_CHAR(uploadtime, 'TMMonth')
                          ORDER BY year, month_num";

                var result = await _database.FetchAsync<dynamic>(sql, parameters.ToArray());

                var fallbackTotalUsed = result.Any() ? ConvertToInt(result.First().total_used) : 0;
                var monthlyUsage = result.Select(item => new MonthlyLicenseUsage
                {
                    Month = $"T{(int)(decimal)item.month_num}/{(int)(decimal)item.year}",
                    Usage = ConvertToInt(item.usage)
                }).ToList();

                if (!monthlyUsage.Any())
                {
                    var currentDate = fromDate;
                    while (currentDate <= toDate)
                    {
                        monthlyUsage.Add(new MonthlyLicenseUsage
                        {
                            Month = $"T{currentDate.Month}/{currentDate.Year}",
                            Usage = 0
                        });
                        currentDate = currentDate.AddMonths(1);
                    }
                }

                var used = hasLedgerData ? ledgerTotalUsed : fallbackTotalUsed;
                var total = hasLedgerData ? ledgerTotalPurchased : 0;
                var percentUsed = total > 0 ? Math.Round((double)used / total * 100, 1) : 0;

                return new LicenseData
                {
                    Used = used,
                    Total = total,
                    PercentUsed = percentUsed,
                    MonthlyUsage = monthlyUsage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi LicenseData - SQL: {SQL}", sql);
                var used = hasLedgerData ? ledgerTotalUsed : 0;
                var total = hasLedgerData ? ledgerTotalPurchased : 0;
                var percentUsed = total > 0 ? Math.Round((double)used / total * 100, 1) : 0;

                return new LicenseData
                {
                    Used = used,
                    Total = total,
                    PercentUsed = percentUsed,
                    MonthlyUsage = new List<MonthlyLicenseUsage>()
                };
            }
        }

        private async Task<HeatmapData> GetHeatmapDataInternalAsync(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var templates = new List<string> { "TT200", "TT133B01", "TT133B01a", "TT133B01b", "Unknown" };
                var completedStatus = (int)OcrFileStatus.Completed;

                // 1. Tạo danh sách 6 tháng cố định (Tính từ toDate lùi về quá khứ)
                var selectedMonths = new List<(int Year, int Month)>();
                for (int i = 5; i >= 0; i--)
                {
                    var d = toDate.AddMonths(-i);
                    selectedMonths.Add((d.Year, d.Month));
                }

                var periods = selectedMonths.Select(m => $"T{m.Month}/{m.Year}").ToList();

                // 2. Query lấy dữ liệu thực tế
                var sql = $@"
            SELECT 
                EXTRACT(MONTH FROM uploadtime) AS month_num,
                EXTRACT(YEAR FROM uploadtime) AS year,
                COALESCE(circular, typeocr, 'Unknown') AS template,
                COUNT(*) AS total,
                SUM(CASE WHEN statusocr = {completedStatus} THEN 1 ELSE 0 END) AS success,
                AVG(CASE WHEN pagecount > 0 AND timeocr IS NOT NULL AND NULLIF(timeocr::text, '') IS NOT NULL 
                    THEN EXTRACT(EPOCH FROM timeocr::interval) / pagecount END) AS avg_time,
                AVG(COALESCE(accuracyrate, 0)) AS avg_accuracy
            FROM {TableName}
            WHERE uploadtime >= @0 AND uploadtime < @1
            GROUP BY 2, 1, 3";

                var dbData = await _database.FetchAsync<dynamic>(sql, fromDate, toDate);

                // Tạo Dictionary để tra cứu nhanh dữ liệu từ DB
                var dataLookup = dbData.ToDictionary(
                    d => ($"{(int)(decimal)d.year}-{(int)(decimal)d.month_num}", (string)d.template),
                    d => d
                );

                var accuracyData = new List<List<double>>();
                var processingTimeData = new List<List<double>>();

                // 3. Map dữ liệu vào Grid cố định (Padding số 0 nếu thiếu)
                foreach (var template in templates)
                {
                    var accuracyRow = new List<double>();
                    var timeRow = new List<double>();

                    foreach (var month in selectedMonths)
                    {
                        var key = ($"{month.Year}-{month.Month}", template);
                        if (dataLookup.TryGetValue(key, out var stat))
                        {
                            accuracyRow.Add(Math.Round(stat.avg_accuracy != null ? (double)stat.avg_accuracy : 0, 1));
                            timeRow.Add(Math.Round(stat.avg_time != null ? (double)stat.avg_time : 0, 1));
                        }
                        else
                        {
                            accuracyRow.Add(0); // Điền 0 nếu tháng này template này không có dữ liệu
                            timeRow.Add(0);
                        }
                    }
                    accuracyData.Add(accuracyRow);
                    processingTimeData.Add(timeRow);
                }

                return new HeatmapData
                {
                    Periods = periods,
                    Templates = templates,
                    Accuracy = accuracyData,
                    ProcessingTime = processingTimeData
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi Heatmap Padding");
                return new HeatmapData { /* return empty object as fallback */ };
            }
        }

        private async Task<PerformanceTrendsData> GetPerformanceTrendsDataInternalAsync(
    DateTime fromDate, DateTime toDate, string whereClause, List<object> parameters)
        {
            var sql = "";
            try
            {
                var completedStatus = (int)OcrFileStatus.Completed;

                sql = $@"
                    SELECT 
                           EXTRACT(MONTH FROM uploadtime) AS month_num,
                                 EXTRACT(YEAR FROM uploadtime) AS year,
                     COUNT(*) as total_files,
                      SUM(CASE WHEN statusocr = {completedStatus} THEN 1 ELSE 0 END) as success_files,
                         COALESCE(SUM(pagecount), 0) AS total_pages,
                             AVG(CASE 
                              WHEN pagecount > 0 AND timeocr IS NOT NULL AND NULLIF(timeocr::text, '') IS NOT NULL AND statusocr = {completedStatus}
                        THEN EXTRACT(EPOCH FROM timeocr::interval) / pagecount
                      ELSE NULL
                      END) as avg_speed,
                       AVG(COALESCE(accuracyrate, 0)) as avg_accuracy
                     FROM {TableName}
                     WHERE uploadtime >= @0
                            AND uploadtime < @1
                               {whereClause}
                       GROUP BY EXTRACT(YEAR FROM uploadtime), EXTRACT(MONTH FROM uploadtime)
                             ORDER BY year, month_num";

                var result = await _database.FetchAsync<dynamic>(sql, parameters.ToArray());

                var monthlyTrends = result.Select(item =>
           {
               var monthNum = (int)(decimal)item.month_num;
               var year = (int)(decimal)item.year;
               var totalFiles = (long)item.total_files;
               var successFiles = (long)item.success_files;
               var totalPages = (long)item.total_pages;
               var avgSpeed = item.avg_speed != null ? (double)item.avg_speed : 0;
               var avgAccuracy = item.avg_accuracy != null ? (double)item.avg_accuracy : 0;
               var successRate = totalFiles > 0 ? Math.Round((double)successFiles / totalFiles * 100, 1) : 0;

               return new MonthlyPerformanceTrend
               {
                   Month = $"T{monthNum}/{year}",
                   SuccessRate = successRate,
                   Accuracy = Math.Round(avgAccuracy, 1),
                   AvgSpeed = Math.Round(avgSpeed, 1),
                   Licenses = (int)totalPages
               };
           }).ToList();

                if (!monthlyTrends.Any())
                {
                    var currentDate = fromDate;
                    while (currentDate <= toDate)
                    {
                        monthlyTrends.Add(new MonthlyPerformanceTrend
                        {
                            Month = $"T{currentDate.Month}/{currentDate.Year}",
                            SuccessRate = 0,
                            Accuracy = 0,
                            AvgSpeed = 0,
                            Licenses = 0
                        });
                        currentDate = currentDate.AddMonths(1);
                    }
                }

                return new PerformanceTrendsData
                {
                    MonthlyTrends = monthlyTrends
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi PerformanceTrends - SQL: {SQL}", sql);
                return new PerformanceTrendsData
                {
                    MonthlyTrends = new List<MonthlyPerformanceTrend>()
                };
            }
        }

        private async Task<TemplateAnalysisData> GetTemplateAnalysisDataInternalAsync(DateTime fromDate, DateTime toDate)
        {
            var sql = "";
            try
            {
                var completedStatus = (int)OcrFileStatus.Completed;
                var templateOrder = new[] { "TT200", "TT133B01", "TT133B01a", "TT133B01b", "Unknown" };

                sql = $@"
                  SELECT 
                        COALESCE(circular, typeocr, 'Unknown') AS template_name,
                  COUNT(*) as total_files,
                          SUM(CASE WHEN statusocr = {completedStatus} THEN 1 ELSE 0 END) as success_files,
                   AVG(COALESCE(accuracyrate, 0)) as avg_accuracy,
                        AVG(CASE 
                        WHEN pagecount > 0 AND timeocr IS NOT NULL AND NULLIF(timeocr::text, '') IS NOT NULL
                 THEN EXTRACT(EPOCH FROM timeocr::interval) / pagecount
                          ELSE NULL
                   END) as avg_processing_time
                FROM {TableName}
                WHERE uploadtime >= @0
                        AND uploadtime < @1
                   GROUP BY COALESCE(circular, typeocr, 'Unknown')";

                var result = await _database.FetchAsync<dynamic>(sql, fromDate, toDate);
                var dataLookup = result.ToDictionary(d => (string)d.template_name, d => d);

                var templates = templateOrder.Select(templateName =>
                  {
                      if (dataLookup.TryGetValue(templateName, out var stat) && (long)stat.total_files > 0)
                      {
                          var totalFiles = (long)stat.total_files;
                          var successFiles = (long)stat.success_files;
                          var avgAccuracy = stat.avg_accuracy != null ? (double)stat.avg_accuracy : 0;
                          var avgProcessingTime = stat.avg_processing_time != null ? (double)stat.avg_processing_time : 0;
                          var successRate = totalFiles > 0 ? Math.Round((double)successFiles / totalFiles * 100, 1) : 0;

                          return new TemplateStats
                          {
                              TemplateName = templateName,
                              TotalFiles = (int)totalFiles,
                              SuccessRate = successRate,
                              AvgAccuracy = Math.Round(avgAccuracy, 1),
                              AvgProcessingTime = Math.Round(avgProcessingTime, 1)
                          };
                      }

                      return new TemplateStats
                      {
                          TemplateName = templateName,
                          TotalFiles = 0,
                          SuccessRate = 0,
                          AvgAccuracy = 0,
                          AvgProcessingTime = 0
                      };
                  }).ToList();

                return new TemplateAnalysisData
                {
                    Templates = templates
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi TemplateAnalysis - SQL: {SQL}", sql);
                return new TemplateAnalysisData
                {
                    Templates = new List<TemplateStats>()
                };
            }
        }

        // Keep existing public methods for backward compatibility
        public async Task<OcrOverviewStats> GetOcrOverviewStatsAsync(FilterModel filters)
        {
            try
            {
                var (fromDate, toDate) = ParseDateRange(filters);
                var whereClause = BuildWhereClause(filters);
                var parameters = BuildParameters(filters, fromDate, toDate);
                return await GetOcrOverviewStatsInternalAsync(fromDate, toDate, whereClause, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting OCR overview stats");
                throw;
            }
        }

        public async Task<List<ScatterData>> GetScatterDataAsync(FilterModel filters)
        {
            try
            {
                var (fromDate, toDate) = ParseDateRange(filters);
                var whereClause = BuildWhereClause(filters);
                var parameters = BuildParameters(filters, fromDate, toDate);
                return await GetScatterDataInternalAsync(fromDate, toDate, whereClause, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting scatter data");
                return new List<ScatterData>();
            }
        }

        public async Task<HeatmapData> GetHeatmapDataAsync(FilterModel filters)
        {
            try
            {
                var (fromDate, toDate) = ParseDateRange(filters);
                return await GetHeatmapDataInternalAsync(fromDate, toDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting heatmap data");
                return new HeatmapData
                {
                    Periods = new List<string> { "No Data" },
                    Templates = new List<string> { "No Data" },
                    Accuracy = new List<List<double>> { new List<double> { 0 } },
                    ProcessingTime = new List<List<double>> { new List<double> { 0 } }
                };
            }
        }

        public async Task<List<FileRecord>> GetWorstAccuracyFilesAsync(FilterModel filters, int top = 10)
        {
            try
            {
                var (fromDate, toDate) = ParseDateRange(filters);
                var whereClause = BuildWhereClause(filters);
                var parameters = BuildParameters(filters, fromDate, toDate);
                return await GetWorstAccuracyFilesInternalAsync(fromDate, toDate, whereClause, parameters, top);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting worst accuracy files");
                return new List<FileRecord>();
            }
        }

        public async Task<List<FileRecord>> GetSlowestFilesAsync(FilterModel filters, int top = 10)
        {
            try
            {
                var (fromDate, toDate) = ParseDateRange(filters);
                var whereClause = BuildWhereClause(filters);
                var parameters = BuildParameters(filters, fromDate, toDate);
                return await GetSlowestFilesInternalAsync(fromDate, toDate, whereClause, parameters, top);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting slowest files");
                return new List<FileRecord>();
            }
        }

        #region Helper Methods

        private (DateTime fromDate, DateTime toDate) ParseDateRange(FilterModel filters)
        {
            // Sử dụng các method từ FilterModel để đảm bảo format nhất quán
            var fromDate = filters.GetFromDateTime();
            var toDate = filters.GetToDateTime();

            return (fromDate, toDate);
        }

        private string BuildWhereClause(FilterModel filters)
        {
            var conditions = new List<string>();

            // Template filter
            if (!string.IsNullOrEmpty(filters.Template) && filters.Template != "all")
            {
                if (filters.Template == "TT133")
                {
                    // TT133 lấy từ circular
                    conditions.Add("AND circular = @2");
                }
                else if (filters.Template == "Unknown")
                {
                    // Unknown: cả circular và typeocr đều NULL
                    conditions.Add("AND circular IS NULL AND typeocr IS NULL");
                }
                else
                {
                    // Các template khác lấy từ typeocr
                    conditions.Add("AND typeocr = @2");
                }
            }

            // Status filter
            if (!string.IsNullOrEmpty(filters.Status) && filters.Status != "all")
            {
                if (filters.Status == "success")
                {
                    conditions.Add("AND statusocr = @3"); // Completed = 4
                }
                else if (filters.Status == "failed")
                {
                    conditions.Add("AND statusocr = @3"); // Failed = 3
                }
                else if (filters.Status == "manual")
                {
                    conditions.Add("AND circular IS NOT NULL");
                }
            }

            return string.Join(" ", conditions);
        }

        private List<object> BuildParameters(FilterModel filters, DateTime fromDate, DateTime toDate)
        {
            var parameters = new List<object> { fromDate, toDate };

            // Template parameter
            if (!string.IsNullOrEmpty(filters.Template) && filters.Template != "all")
            {
                if (filters.Template == "Unknown")
                {
                    // Unknown không cần parameter vì dùng IS NULL
                    parameters.Add(DBNull.Value);
                }
                else
                {
                    // TT133 hoặc các template khác
                    parameters.Add(filters.Template);
                }
            }
            else
            {
                parameters.Add(DBNull.Value);
            }

            // Status parameter
            if (!string.IsNullOrEmpty(filters.Status) && filters.Status != "all")
            {
                if (filters.Status == "success")
                {
                    parameters.Add((int)OcrFileStatus.Completed); // 4
                }
                else if (filters.Status == "failed")
                {
                    parameters.Add(3); // Failed = 3
                }
                else if (filters.Status == "manual")
                {
                    // Manual không cần parameter vì dùng IS NOT NULL
                    parameters.Add(DBNull.Value);
                }
                else
                {
                    parameters.Add(DBNull.Value);
                }
            }
            else
            {
                parameters.Add(DBNull.Value);
            }

            return parameters;
        }

        /// <summary>
        /// Lấy danh sách các giá trị status tương ứng với trạng thái failed
        /// </summary>
        private static List<int> GetFailedStatusValues()
        {
            return new List<int>
            {
                (int)OcrFileStatus.FileNotFound,
                (int)OcrFileStatus.PendingExtraction,
                (int)OcrFileStatus.EmptyExtraction,
                (int)OcrFileStatus.NoCircular,
                (int)OcrFileStatus.InvalidDIP,
                (int)OcrFileStatus.FilePassword
            };
        }

        private static int ConvertToInt(object? value)
        {
            if (value == null || value is DBNull)
            {
                return 0;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return 0;
            }
        }

        #endregion

        public async Task<LicenseData> GetLicenseDataAsync(FilterModel filters)
        {
            try
            {
                var (fromDate, toDate) = ParseDateRange(filters);
                var whereClause = BuildWhereClause(filters);
                var parameters = BuildParameters(filters, fromDate, toDate);
                return await GetLicenseDataInternalAsync(fromDate, toDate, whereClause, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting license data");
                return new LicenseData
                {
                    Used = 0,
                    Total = 0,
                    PercentUsed = 0,
                    MonthlyUsage = new List<MonthlyLicenseUsage>()
                };
            }
        }

        public async Task<PerformanceTrendsData> GetPerformanceTrendsDataAsync(FilterModel filters)
        {
            try
            {
                var (fromDate, toDate) = ParseDateRange(filters);
                var whereClause = BuildWhereClause(filters);
                var parameters = BuildParameters(filters, fromDate, toDate);
                return await GetPerformanceTrendsDataInternalAsync(fromDate, toDate, whereClause, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting performance trends data");
                return new PerformanceTrendsData
                {
                    MonthlyTrends = new List<MonthlyPerformanceTrend>()
                };
            }
        }

        public async Task<TemplateAnalysisData> GetTemplateAnalysisDataAsync(FilterModel filters)
        {
            try
            {
                var (fromDate, toDate) = ParseDateRange(filters);
                return await GetTemplateAnalysisDataInternalAsync(fromDate, toDate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting template analysis data");
                return new TemplateAnalysisData
                {
                    Templates = new List<TemplateStats>()
                };
            }
        }
    }
}
