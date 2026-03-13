using System;
using PetaPoco;

namespace OcrDashboardMvc.Models
{
    //[TableName("SFTPOcrFile")]
    //[PrimaryKey("ID", AutoIncrement = true)]
    //public class SFTPOcrFile : DocProTrungGianDataContext<SFTPOcrFile>
    //{
    //    public long ID { get; set; }

    //    public string FileName { get; set; }

    //    public int StatusOCR { get; set; }

    //    public string PathInput { get; set; }

    //    public string PathOutput { get; set; }

    //    public DateTime? Created { get; set; }

    //    public DateTime? Updated { get; set; }

    //    public int StatusUpdate { get; set; }

    //    public string Circular { get; set; } // Thông tư

    //    public string DIP { get; set; }

    //    public string Method { get; set; }

    //    public string TypeOCR { get; set; } // Loại bóc tách 

    //    public DateTime? DateRetry { get; set; } // Thời gian chạy lại tiếp theo nếu lỗi

    //    public int CountRetry { get; set; } // Số lần đã chạy

    //    public int PageCount { get; set; }

    //    public int StatusCheck { get; set; } // Trạng thái check

    //    public string TimeOCR { get; set; }
    //}

    [TableName("public.ocr_requests")] //ocr_clos.sftpocrfile
    [PrimaryKey("id", AutoIncrement = true)]
    public class SFTPOcrFile : DocProTrungGianDataContext<SFTPOcrFile>
    {
        [Column("id")]
        public long ID { get; set; }
        [Column("filename")]
        public string FileName { get; set; }
        [Column("statusocr")]
        public OcrFileStatus StatusOCR { get; set; }
        [Column("pathinput")]
        public string PathInput { get; set; }
        [Column("pathoutput")]
        public string PathOutput { get; set; }
        [Column("created")]
        public DateTime? Created { get; set; }
        [Column("updated")]
        public DateTime? Updated { get; set; } 
        [Column("uploadtime")]
        public DateTime? UploadTime { get; set; }
        [Column("statusupdate")]
        public int StatusUpdate { get; set; }
        [Column("circular")]
        public string Circular { get; set; }//thông tư
        [Column("dip")]
        public string DIP { get; set; }
        [Column("method")]
        public string Method { get; set; }
        [Column("typeocr")]
        public string TypeOCR { get; set; }//loại bóc tách 
        [Column("dateretry")]
        public DateTime? DateRetry { get; set; }//thời gian chạy lại tiếp theo nếu lỗi
        [Column("countretry")]
        public int CountRetry { get; set; }//số lần đã chạy
        [Column("pagecount")]
        public int PageCount { get; set; }
        [Column("statuscheck")]
        public int StatusCheck { get; set; }//số lần đã chạy
        [Column("timeocr")]
        public string TimeOCR { get; set; }
        [Column("status")]
        public int Status { get; set; }//trạng thái tôgnr quan
        [Column("accuracyrate")]
        public double AccuracyRate { get; set; }//Tỷ lệ chính xác
    }
}
