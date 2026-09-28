# HƯỚNG DẪN CÀI ĐẶT & CHẠY DỰ ÁN SmartIELTS
> **Hệ thống Khảo thí Tiếng Anh Toàn diện IELTS 4 Kỹ năng (SWD392 & PRN222)**  
> Solution: [`SmartIELTS.sln`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/SmartIELTS.sln) • Nền tảng: **.NET 8**

---

## 1. Danh Sách Tài Khoản Mẫu Đã Nạp Sẵn (Seed Data)

Khi khởi động ứng dụng lần đầu, hệ thống tự động sinh cơ sở dữ liệu SQLite (`smartielts_system.db`) và nạp sẵn các tài khoản:

| Vai trò | Tên đăng nhập (Username) | Mật khẩu (Password) | Mục đích sử dụng |
| :--- | :--- | :--- | :--- |
| **Thí sinh (Candidate)** | `student` | `Student@123` | Đăng nhập phòng thi Kiosk, làm bài 4 kỹ năng, xem báo cáo chẩn đoán |
| **Giảng viên (Teacher)** | `teacher` | `Teacher@123` | Mở Review Dashboard, duyệt điểm gợi ý của AI (Human-in-the-loop) |
| **Quản trị viên (Admin)** | `admin` | `Admin@123` | Quản lý đề thi và ca thi toàn trường |

---

## 2. Cách Chạy Trên Visual Studio (Giao Diện Đồ Họa)

### Bước 1: Mở Solution
1. Mở phần mềm **Visual Studio 2022** (hoặc mới hơn).
2. Chọn **Open a project or solution**, duyệt tới thư mục `SmartIELTS_Project` và mở file:
   [`SmartIELTS.sln`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/SmartIELTS.sln) (hoặc [`SmartIELTS.slnx`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/SmartIELTS.slnx)).

---

### Bước 2: Chọn Dự Án Muốn Khởi Chạy

#### Cách A: Chạy Ứng Dụng Máy Trạm Kiosk Mode (Khuyên dùng khi chấm bài & demo PRN222)
1. Trong cửa sổ **Solution Explorer**, click chuột phải vào project `SmartIELTS.WpfApp`.
2. Chọn **Set as Startup Project**.
3. Bấm phím **F5** (hoặc nút **Start / Debug** màu xanh trên thanh công cụ).
4. Ứng dụng sẽ hiển thị toàn màn hình (Kiosk Mode):
   - Đăng nhập bằng tài khoản: `student` / `Student@123`.
   - Bấm **"Vào Phòng Thi Khóa Máy (Bắt Đầu Ca Thi)"**.
   - Trải nghiệm lần lượt:
     * **1. Listening:** Nghe audio 1 chiều (khóa tua), điền đáp án (hỗ trợ chấm dung sai hoa/thường, biến thể Anh-Mỹ).
     * **2. Reading:** Giao diện chia đôi màn hình (Split-Screen), bên trái là bài đọc có công cụ Highlight, bên phải là bảng câu hỏi.
     * **3. Writing:** Soạn bài luận, đếm số từ theo thời gian thực (Live Word Count), thử bấm `Ctrl+V` để thấy hệ thống khóa lệnh Paste từ clipboard.
     * **4. Speaking:** Mô phỏng ghi âm trả lời, AI chấm âm vị và sinh câu hỏi phản biện thích ứng ở Part 3 (Adaptive follow-up).
     * **5. Báo cáo chẩn đoán:** Xem bảng điểm tổng quan Overall Band và nhận xét chi tiết.
   - Thử bấm `Alt+Tab` hoặc phím `Windows` để kiểm tra tính năng chặn phím nóng của Kiosk Mode.

---

#### Cách B: Chạy Backend Web API (Dành cho kiểm tra RESTful & Swagger UI)
1. Trong cửa sổ **Solution Explorer**, click chuột phải vào project `SmartIELTS.WebApi`.
2. Chọn **Set as Startup Project**.
3. Bấm phím **F5**.
4. Trình duyệt tự động mở trang **Swagger UI** tại địa chỉ:
   `http://localhost:5294/`
5. Danh sách các API chính có thể test trực tiếp trên Swagger:
   - `POST /api/auth/login`: Đăng nhập lấy thông tin người dùng.
   - `GET /api/exams`: Lấy danh sách đề thi IELTS đã seed sẵn.
   - `POST /api/sessions/start`: Khởi tạo ca thi mới cho thí sinh.
   - `POST /api/sessions/submit-skill`: Nộp bài từng kỹ năng và kích hoạt Strategy Pattern chấm điểm.
   - `GET /api/grading/pending-reviews`: Danh sách bài thi chờ Giảng viên duyệt điểm.
   - `POST /api/grading/approve-score`: Giảng viên điều chỉnh và chốt điểm chính thức.

---

## 3. Cách Chạy Bằng Dòng Lệnh (Terminal / Command Line)

Nếu không dùng Visual Studio hoặc muốn chạy nhanh bằng PowerShell / CMD:

### Chạy Máy Trạm WPF Desktop:
```powershell
dotnet run --project src/SmartIELTS.WpfApp/SmartIELTS.WpfApp.csproj
```

### Chạy Backend Web API (Swagger UI):
```powershell
dotnet run --project src/SmartIELTS.WebApi/SmartIELTS.WebApi.csproj
```

---

## 4. Cơ Sở Dữ Liệu Riêng SQL Server (`SmartIELTS_DB`) & Cấu Hình Kết Nối

Hệ thống đã được thiết lập sẵn một cơ sở dữ liệu riêng trên **Microsoft SQL Server**:
- **Tên Database:** `SmartIELTS_DB`
- **Địa chỉ máy chủ (Server):** `localhost` (hoặc `.` / `(local)`)
- **Xác thực:** Windows Authentication (`Trusted_Connection=True;TrustServerCertificate=True`)
- **Tệp Script SQL hoàn chỉnh:** [`SmartIELTS_Database.sql`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/SmartIELTS_Database.sql) (Có thể mở bằng SQL Server Management Studio - SSMS để xem toàn bộ 8 bảng và dữ liệu mẫu).

### Cấu hình kết nối trong dự án:
Cả hai ứng dụng `SmartIELTS.WebApi` và `SmartIELTS.WpfApp` đều đã được cấu hình trỏ thẳng vào `SmartIELTS_DB`:
```json
{
  "DatabaseProvider": "SqlServer",
  "ConnectionStrings": {
    "SqlServerConnection": "Server=localhost;Database=SmartIELTS_DB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true",
    "SqliteConnection": "Data Source=smartielts_system.db"
  }
}
```
*(Nếu muốn chuyển sang chạy SQLite offline mang đi nộp máy khác không có SQL Server, chỉ cần sửa `"DatabaseProvider": "Sqlite"`).*

---

## 5. Kịch Bản Trình Diễn Khi Chấm Bài (15 Phút)

1. **Phút 0 - 3 (Kiến trúc SWD392):** Mở Visual Studio giới thiệu cấu trúc Clean Architecture phân tầng, chỉ ra nơi triển khai Strategy Pattern cho 4 kỹ năng chấm thi tại `SmartIELTS.Application/GradingStrategies/`.
2. **Phút 3 - 6 (Bảo mật & Kiosk PRN222):** Chạy `SmartIELTS.WpfApp`, bấm Alt+Tab và phím Windows trực tiếp trước mặt hội đồng để chứng minh hệ thống đã khóa cứng phím tắt bằng Win32 Low-Level Keyboard Hook. Thử Paste văn bản vào Writing để minh chứng chống gian lận.
3. **Phút 6 - 12 (Trải nghiệm bài thi 4 kỹ năng):** Thao tác qua 4 phần thi Listening, Reading chia đôi màn hình, Writing đếm từ trực tiếp, và Speaking AI.
4. **Phút 12 - 15 (Duyệt điểm Human-in-the-loop & Báo cáo):** Xem bản Báo cáo chẩn đoán Band Score, bài mẫu Band 8.0+ do AI viết lại và câu hỏi xoáy Part 3.
