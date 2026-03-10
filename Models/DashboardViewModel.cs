using System.Globalization;

namespace OcrDashboardMvc.Models
{
    public class FilterModel
    {
        private const string DateFormat = "dd/MM/yyyy";

        public string FromDate { get; set; } 
        public string ToDate { get; set; }
        public string Template { get; set; } = "all";
        public string Status { get; set; } = "all";
        
        public FilterModel()
        {
            var now = DateTime.Now;
            FromDate = new DateTime(now.Year, 1, 1).ToString(DateFormat, CultureInfo.InvariantCulture);
            ToDate = now.ToString(DateFormat, CultureInfo.InvariantCulture);
        }
        
        /// <summary>
        /// Parse FromDate string to DateTime, always using dd/MM/yyyy format
        /// </summary>
        public DateTime GetFromDateTime()
        {
            if (string.IsNullOrWhiteSpace(FromDate))
            {
                return new DateTime(DateTime.Now.Year, 1, 1);
            }
            
            if (DateTime.TryParseExact(FromDate, DateFormat, CultureInfo.InvariantCulture, 
                DateTimeStyles.None, out DateTime result))
            {
                return result;
            }
   
            // Fallback for other formats
            if (DateTime.TryParse(FromDate, out result))
            {
                return result;
            }
            
            return new DateTime(DateTime.Now.Year, 1, 1);
        }
        
        /// <summary>
        /// Parse ToDate string to DateTime, always using dd/MM/yyyy format
        /// </summary>
        public DateTime GetToDateTime()
        {
            if (string.IsNullOrWhiteSpace(ToDate))
            {
                return DateTime.Now;
            }
            
            if (DateTime.TryParseExact(ToDate, DateFormat, CultureInfo.InvariantCulture, 
                DateTimeStyles.None, out DateTime result))
            {
                return result;
            }
        
            // Fallback for other formats
            if (DateTime.TryParse(ToDate, out result))
            {
                return result;
            }
  
            return DateTime.Now;
        }
    }

    public class DashboardViewModel
    {
        public FilterModel Filters { get; set; }
        public OcrOverviewStats Stats { get; set; }
        public List<ScatterData> ScatterData { get; set; }
        public HeatmapData HeatmapData { get; set; }
        public List<FileRecord> WorstAccuracyFiles { get; set; }
        public List<FileRecord> SlowestFiles { get; set; }
        public LicenseData LicenseData { get; set; }
        public PerformanceTrendsData PerformanceTrendsData { get; set; }
        public TemplateAnalysisData TemplateAnalysisData { get; set; }
    }

    public class OcrOverviewStats
    {
        public int TotalReports { get; set; }
        public int OcrSuccess { get; set; }
        public int OcrFailed { get; set; }
        public int OcrManual { get; set; }
        public int TotalPages { get; set; }
        public double SuccessRate { get; set; }
        public double AccuracyRate { get; set; }
        public double AvgProcessingSpeed { get; set; }
    }

    public class LicenseData
    {
        public int Used { get; set; }
        public int Total { get; set; }
        public double PercentUsed { get; set; }
        public List<MonthlyLicenseUsage> MonthlyUsage { get; set; }
    }

    public class MonthlyLicenseUsage
    {
        public string Month { get; set; }
        public int Usage { get; set; }
    }

    public class ScatterData
    {
        public double Accuracy { get; set; }
        public double Time { get; set; }
        public string Template { get; set; }
        public string Id { get; set; }
    }

    public class HeatmapData
    {
        public List<string> Periods { get; set; }
        public List<string> Templates { get; set; }
        public List<List<double>> Accuracy { get; set; }
        public List<List<double>> ProcessingTime { get; set; }
    }

    public class FileRecord
    {
        public string RecordCode { get; set; }
        public string Template { get; set; }
        public string OcrDate { get; set; }
        public double Accuracy { get; set; }
        public double ProcessingTime { get; set; }
        public int Pages { get; set; }
    }

    public class PerformanceTrendsData
    {
        public List<MonthlyPerformanceTrend> MonthlyTrends { get; set; }
    }

    public class MonthlyPerformanceTrend
    {
        public string Month { get; set; }
        public double SuccessRate { get; set; }
        public double Accuracy { get; set; }
        public double AvgSpeed { get; set; }
        public int Licenses { get; set; }
    }

    public class TemplateAnalysisData
    {
        public List<TemplateStats> Templates { get; set; }
    }

    public class TemplateStats
    {
        public string TemplateName { get; set; }
        public int TotalFiles { get; set; }
        public double SuccessRate { get; set; }
        public double AvgAccuracy { get; set; }
        public double AvgProcessingTime { get; set; }
    }
}
