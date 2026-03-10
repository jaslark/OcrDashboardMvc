using System.ComponentModel;

namespace OcrDashboardMvc.Models
{
    /// <summary>
    /// Enum định nghĩa các trạng thái của file OCR
    /// </summary>
    public enum OcrFileStatus
    {
        /// <summary>
        /// File vật lý không tồn tại
        /// </summary>
        [Description("File vật lý không tồn tại")]
        FileNotFound = 0,

        /// <summary>
        /// Chưa bóc tách
        /// </summary>
        [Description("Chưa bóc tách")]
        PendingExtraction = 1,

        /// <summary>
        /// Bóc tách trống
        /// </summary>
        [Description("Bóc tách trống")]
        EmptyExtraction = 2,

        /// <summary>
        /// Không có thông tư
        /// </summary>
        [Description("Không có thông tư")]
        NoCircular = 3,

        /// <summary>
        /// Hoàn thành
        /// </summary>
        [Description("Hoàn thành")]
        Completed = 4,

        /// <summary>
        /// DIP không đủ điều kiện
        /// </summary>
        [Description("DIP không đủ điều kiện")]
        InvalidDIP = 5,

        /// <summary>
        /// File PDF bị khóa mật khẩu
        /// </summary>
        [Description("File PDF bị khóa mật khẩu")]
        FilePassword = 6
    }
}
