using OcrDashboardMvc.Models;
using PetaPoco;
using System.Globalization;

namespace OcrDashboardMvc.Services
{
    public interface IDashboardService
    {
        Task<DashboardDataBundle> GetAllDashboardDataAsync(FilterModel filters, int totalLicensePages);
        Task<OcrOverviewStats> GetOcrOverviewStatsAsync(FilterModel filters);
        Task<List<ScatterData>> GetScatterDataAsync(FilterModel filters);
        Task<HeatmapData> GetHeatmapDataAsync(FilterModel filters);
        Task<List<FileRecord>> GetWorstAccuracyFilesAsync(FilterModel filters, int top = 10);
        Task<List<FileRecord>> GetSlowestFilesAsync(FilterModel filters, int top = 10);
        Task<LicenseData> GetLicenseDataAsync(FilterModel filters, int totalLicensePages);
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
        private readonly IDatabase _database;
        private readonly ILogger<DashboardService> _logger;
        private const string TableName = "ocr_clos.sftpocrfile";

        public DashboardService(IDatabase database, ILogger<DashboardService> logger)
        {
            _database = database;
            _logger = logger;
        }

        /// <summary>
        /// Optimized method to get all dashboard data with minimal database round trips
        /// </summary>
        public async Task<DashboardDataBundle> GetAllDashboardDataAsync(FilterModel filters, int totalLicensePages)
        {
            try
            {
                var (fromDate, toDate) = ParseDateRange(filters);

                var whereClause = BuildWhereClause(filters);
                var parameters = BuildParameters(filters, fromDate, toDate);

                // Execute queries sequentially
                var stats = await GetOcrOverviewStatsInternalAsync(fromDate, toDate, whereClause, parameters);
                var scatterData = await GetScatterDataInternalAsync(fromDate, toDate, whereClause, parameters);
                var worstAccuracyFiles = await GetWorstAccuracyFilesInternalAsync(fromDate, toDate, whereClause, parameters, 10);
                var slowestFiles = await GetSlowestFilesInternalAsync(fromDate, toDate, whereClause, parameters, 10);
                var licenseData = await GetLicenseDataInternalAsync(fromDate, toDate, whereClause, parameters, totalLicensePages);
                var heatmapData = await GetHeatmapDataInternalAsync(fromDate, toDate);
                var performanceTrendsData = await GetPerformanceTrendsDataInternalAsync(fromDate, toDate, whereClause, parameters);
                var templateAnalysisData = await GetTemplateAnalysisDataInternalAsync(fromDate, toDate);

                return new DashboardDataBundle
                {
                    Stats = stats,
                    ScatterData = scatterData,
                    HeatmapData = heatmapData,
                    WorstAccuracyFiles = worstAccuracyFiles,
                    SlowestFiles = slowestFiles,
                    LicenseData = licenseData,
                    PerformanceTrendsData = performanceTrendsData,
                    TemplateAnalysisData = templateAnalysisData
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi SERVICE - GetAllDashboardDataAsync: {Message}", ex.Message);
                throw;
            }
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
                               WHEN statusocr = {completedStatus} AND pagecount > 0 AND timeocr IS NOT NULL AND timeocr != '' 
                    THEN EXTRACT(EPOCH FROM timeocr::time) / pagecount
                      ELSE NULL
                 END) as avg_speed,
                   AVG(CASE WHEN statusocr = {completedStatus} THEN COALESCE(accuracyrate, 0) ELSE NULL END) as avg_accuracy
                      FROM {TableName}
                        WHERE uploadtime::date BETWEEN @0 AND @1 {whereClause}";

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
   DateTime fromDate, DateTime toDate, string whereClause, List<object> parameters)
        {
            var sql = "";
            try
            {
                sql = $@"
                      SELECT 
                     id::text AS Id,
                  COALESCE(circular, typeocr, 'Unknown') AS Template,
                         CASE 
                      WHEN pagecount > 0 AND timeocr IS NOT NULL AND timeocr != '' 
                  THEN ROUND(CAST(EXTRACT(EPOCH FROM timeocr::time) / pagecount AS numeric), 1)
                  ELSE 0
                           END AS Time,
                       ROUND(CAST(COALESCE(accuracyrate, 0) AS numeric), 1) AS Accuracy
                  FROM {TableName}
                       WHERE uploadtime::date BETWEEN @0 AND @1
                   AND pagecount > 0
                     {whereClause}
                   ORDER BY uploadtime DESC";

                var result = await _database.FetchAsync<ScatterData>(sql, parameters.ToArray());
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
                        WHEN pagecount > 0 AND timeocr IS NOT NULL AND timeocr != '' 
                    THEN ROUND(CAST(EXTRACT(EPOCH FROM timeocr::time) / pagecount AS numeric), 1)
                            ELSE 0
                    END AS ProcessingTime,
                    pagecount AS Pages
                    FROM {TableName}
                    WHERE uploadtime::date BETWEEN @0 AND @1
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
                    WHEN pagecount > 0 AND timeocr IS NOT NULL AND timeocr != '' 
                  THEN ROUND(CAST(EXTRACT(EPOCH FROM timeocr::time) / pagecount AS numeric), 1)
                    ELSE 0
                 END AS ProcessingTime,
                  pagecount AS Pages
                 FROM {TableName}
                  WHERE uploadtime::date BETWEEN @0 AND @1
               AND pagecount > 0
                  AND timeocr IS NOT NULL
                  AND timeocr != ''
               {whereClause}
              ORDER BY ProcessingTime DESC
                 LIMIT @{parameters.Count}";

                return await _database.FetchAsync<FileRecord>(sql, paramsCopy.ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi SlowestFiles - SQL: {SQL}", sql);
                return new List<FileRecord>();
            }
        }

        private async Task<LicenseData> GetLicenseDataInternalAsync(
              DateTime fromDate, DateTime toDate, string whereClause, List<object> parameters, int totalLicensePages)
        {
            var sql = "";
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
                                  WHERE uploadtime::date BETWEEN @0 AND @1
                       AND statusocr = {completedStatus}
                         {whereClause}
                 GROUP BY EXTRACT(YEAR FROM uploadtime), EXTRACT(MONTH FROM uploadtime), TO_CHAR(uploadtime, 'TMMonth')
                          ORDER BY year, month_num";

                var result = await _database.FetchAsync<dynamic>(sql, parameters.ToArray());

                var totalUsedPages = result.Any() ? (int)(long)result.First().total_used : 0;
                var percentUsed = totalLicensePages > 0 ? Math.Round((double)totalUsedPages / totalLicensePages * 100, 1) : 0;

                var monthlyUsage = result.Select(item => new MonthlyLicenseUsage
                {
                    Month = $"T{(int)(decimal)item.month_num}/{(int)(decimal)item.year}",
                    Usage = (int)(long)item.usage
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

                return new LicenseData
                {
                    Used = totalUsedPages,
                    Total = totalLicensePages,
                    PercentUsed = percentUsed,
                    MonthlyUsage = monthlyUsage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi LicenseData - SQL: {SQL}", sql);
                return new LicenseData
                {
                    Used = 0,
                    Total = totalLicensePages,
                    PercentUsed = 0,
                    MonthlyUsage = new List<MonthlyLicenseUsage>()
                };
            }
        }

        private async Task<HeatmapData> GetHeatmapDataInternalAsync(DateTime fromDate, DateTime toDate)
        {
            var sql = "";
            try
            {
                var templates = new List<string> { "TT200", "TT133", "TT133B01", "TT133B01A", "TT133B01B", "Unknown" };
                var completedStatus = (int)OcrFileStatus.Completed;

                sql = $@"
                     SELECT 
                        EXTRACT(MONTH FROM uploadtime) AS month_num,
                      EXTRACT(YEAR FROM uploadtime) AS year,
                 COALESCE(circular, typeocr, 'Unknown') AS template,
                        COUNT(*) as total,
                   SUM(CASE WHEN statusocr = {completedStatus} THEN 1 ELSE 0 END) as success,
                       AVG(CASE 
                        WHEN pagecount > 0 AND timeocr IS NOT NULL AND timeocr != '' 
                    THEN EXTRACT(EPOCH FROM timeocr::time) / pagecount
                     ELSE NULL
                   END) as avg_time,
                       AVG(COALESCE(accuracyrate, 0)) as avg_accuracy
                 FROM {TableName}
                     WHERE uploadtime::date BETWEEN @0 AND @1
                 GROUP BY EXTRACT(YEAR FROM uploadtime), EXTRACT(MONTH FROM uploadtime), 
                   COALESCE(circular, typeocr, 'Unknown')
                      ORDER BY year DESC, month_num DESC";

                var allData = await _database.FetchAsync<dynamic>(sql, fromDate, toDate);

                if (!allData.Any())
                {
                    return new HeatmapData
                    {
                        Periods = new List<string> { "Không có dữ liệu" },
                        Templates = templates,
                        Accuracy = templates.Select(_ => new List<double> { 0 }).ToList(),
                        ProcessingTime = templates.Select(_ => new List<double> { 0 }).ToList()
                    };
                }

                var monthsWithData = allData
              .Select(d => new { Year = (int)(decimal)d.year, Month = (int)(decimal)d.month_num })
               .Distinct()
                   .OrderByDescending(m => m.Year)
                    .ThenByDescending(m => m.Month)
                  .ToList();

                var selectedMonths = monthsWithData.Take(4).OrderBy(m => m.Year).ThenBy(m => m.Month).ToList();
                var periods = selectedMonths.Select(m => $"T{m.Month}/{m.Year}").ToList();

                var dataLookup = allData
             .Where(d => selectedMonths.Any(m =>
            m.Year == (int)(decimal)d.year && m.Month == (int)(decimal)d.month_num))
                    .ToDictionary(
                     d => ($"{(int)(decimal)d.year}-{(int)(decimal)d.month_num}", (string)d.template),
                  d => d
                    );

                var accuracyData = new List<List<double>>();
                var processingTimeData = new List<List<double>>();

                foreach (var template in templates)
                {
                    var accuracyRow = new List<double>();
                    var timeRow = new List<double>();

                    foreach (var month in selectedMonths)
                    {
                        var key = ($"{month.Year}-{month.Month}", template);

                        if (dataLookup.TryGetValue(key, out var stat) && (long)stat.total > 0)
                        {
                            var accuracy = stat.avg_accuracy != null ? (double)stat.avg_accuracy : 0;
                            accuracyRow.Add(Math.Round(accuracy, 1));
                            timeRow.Add(stat.avg_time != null ? Math.Round((double)stat.avg_time, 1) : 0);
                        }
                        else
                        {
                            accuracyRow.Add(0);
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
                _logger.LogError(ex, "Lỗi HeatmapData - SQL: {SQL}", sql);
                return new HeatmapData
                {
                    Periods = new List<string> { "Error" },
                    Templates = new List<string> { "TT200", "TT133", "TT133B01", "TT133B01A", "TT133B01B", "Unknown" },
                    Accuracy = new List<List<double>> { new List<double> { 0, 0, 0, 0, 0, 0 } },
                    ProcessingTime = new List<List<double>> { new List<double> { 0, 0, 0, 0, 0, 0 } }
                };
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
                             WHEN pagecount > 0 AND timeocr IS NOT NULL AND timeocr != '' AND statusocr = {completedStatus}
                        THEN EXTRACT(EPOCH FROM timeocr::time) / pagecount
                      ELSE NULL
                      END) as avg_speed,
                       AVG(COALESCE(accuracyrate, 0)) as avg_accuracy
                     FROM {TableName}
                     WHERE uploadtime::date BETWEEN @0 AND @1
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
                var templateOrder = new[] { "TT200", "TT133", "TT133B01", "TT133B01A", "TT133B01B", "Unknown" };

                sql = $@"
                  SELECT 
                        COALESCE(circular, typeocr, 'Unknown') AS template_name,
                  COUNT(*) as total_files,
                          SUM(CASE WHEN statusocr = {completedStatus} THEN 1 ELSE 0 END) as success_files,
                   AVG(COALESCE(accuracyrate, 0)) as avg_accuracy,
                        AVG(CASE 
                  WHEN pagecount > 0 AND timeocr IS NOT NULL AND timeocr != '' 
                 THEN EXTRACT(EPOCH FROM timeocr::time) / pagecount
                          ELSE NULL
                   END) as avg_processing_time
                FROM {TableName}
                WHERE uploadtime::date BETWEEN @0 AND @1
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

        #endregion

        public async Task<LicenseData> GetLicenseDataAsync(FilterModel filters, int totalLicensePages)
        {
            try
            {
                var (fromDate, toDate) = ParseDateRange(filters);
                var whereClause = BuildWhereClause(filters);
                var parameters = BuildParameters(filters, fromDate, toDate);
                return await GetLicenseDataInternalAsync(fromDate, toDate, whereClause, parameters, totalLicensePages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting license data");
                return new LicenseData
                {
                    Used = 0,
                    Total = totalLicensePages,
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

