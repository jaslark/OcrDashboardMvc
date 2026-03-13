# Tài liệu Cấu trúc Hệ thống (System Architecture)

Dự án được xây dựng trên nền tảng **ASP.NET Core MVC**, tập trung vào việc quản lý và hiển thị Dashboard dữ liệu từ hệ thống OCR.

---

## 1. Cấu hình Hệ thống (System Overview)
Tệp `Program.cs` đóng vai trò trung tâm điều phối các dịch vụ:
* **Logging:** Sử dụng **Serilog** để ghi vết hệ thống.
* **Localization:** Cấu hình `Vietnamese CultureInfo` cho hiển thị tiền tệ/ngày tháng.
* **Dependency Injection:** Tiêm `IDatabase` (PetaPoco) kết nối qua `TrungGianConnectionString`.
* **Data Binding:** Đăng ký `DateModelBinder` để chuẩn hóa dữ liệu ngày tháng từ Client.

---

## 2. Cấu trúc Thư mục & Thành phần

### 📂 Controllers
* **`HomeController`**: Tiếp nhận yêu cầu trang Dashboard, xử lý `FilterModel` và điều phối dữ liệu từ Service tới View.
* **`SFTPOcrFileController`**: Cung cấp API RESTful (`/api/SFTPOcrFile`) hỗ trợ các thao tác CRUD và phân trang cho ứng dụng bên thứ ba.

### 📂 Models
* **`DashboardViewModel`**: Chứa toàn bộ cấu trúc dữ liệu cho giao diện (thống kê, biểu đồ scatter, heatmap, xu hướng, thông tin bản quyền).
* **`SFTPOcrFile`**: Đối tượng ánh xạ (Mapping) trực tiếp với bảng dữ liệu `ocr_clos.sftpocrfile`.
* **`OcrFileStatus`**: Danh mục (Enum) định nghĩa các trạng thái của tệp OCR.
* **`DocProTrungGianDataContext`**: Các helper dùng chung cho PetaPoco.

### 📂 Services & Repositories
* **`DashboardService`**: Xử lý logic nghiệp vụ phức tạp: xây dựng SQL động, tổng hợp dữ liệu (aggregation), và định dạng dữ liệu cho biểu đồ.
* **`DatabaseService`**: Quản lý việc khởi tạo kết nối PetaPoco.
* **`SFTPOcrFileRepository`**: Đóng gói các truy cập dữ liệu (Data Access) trên bảng `sftpocrfile`.

### 📂 ModelBinders
* **`DateModelBinder`**: Đảm bảo các trường `FromDate` và `ToDate` luôn được format về dạng `dd/MM/yyyy` trước khi truyền vào Controller.

### 📂 Views (UI/UX)
Sử dụng công nghệ **Razor View Engine** kết hợp với bộ thư viện hiện đại:
* **Layout:** `_Layout.cshtml` tích hợp sẵn **Tailwind CSS**, **Lucide Icons**, **Chart.js**, và **Flatpickr**.
* **Partials (Thành phần giao diện):**
    * `_DashboardFilters`: Bộ lọc điều kiện (Ngày, trạng thái, mẫu template).
    * `_OCROverview`: Hiển thị các chỉ số tổng quan (KPI cards).
    * `_PerformanceHeatmap`: Biểu đồ nhiệt theo dõi hiệu suất xử lý.

---

## 3. Luồng hoạt động (MVC Flow)

1.  **Request:** Người dùng truy cập trang chủ hoặc thực hiện lọc dữ liệu.
2.  **Binding:** `DateModelBinder` chuẩn hóa tham số ngày tháng.
3.  **Processing:** * `HomeController` nhận filter và gọi `DashboardService`.
    * `DashboardService` phối hợp với `SFTPOcrFileRepository` để thực thi các truy vấn SQL (Query overview, scatter, heatmap, trend...).
4.  **Mapping:** Dữ liệu thô từ Database được ánh xạ vào `DashboardViewModel`.
5.  **Response:** Giao diện `Index.cshtml` kết hợp các Partial Views để hiển thị biểu đồ và bảng dữ liệu cho người dùng.

---

## 4. Công nghệ sử dụng (Tech Stack)

| Thành phần | Công nghệ |
| :--- | :--- |
| **Backend** | .NET Core MVC |
| **ORM** | PetaPoco |
| **Database** | SQL (Truy vấn qua Connection String) |
| **CSS Framework** | Tailwind CSS |
| **Charts** | Chart.js |
| **Icons** | Lucide |
| **Date Picker** | Flatpickr |