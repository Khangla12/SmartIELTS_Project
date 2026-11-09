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
     * **1. Listening:** Bấm "Phát Audio" để nghe (không có thanh tua), điền đáp án (hỗ trợ chấm dung sai hoa/thường, biến thể Anh-Mỹ).
     * **2. Reading:** Giao diện chia đôi màn hình (Split-Screen), bên trái là bài đọc có công cụ Highlight, bên phải là bảng câu hỏi.
     * **3. Writing:** Soạn bài luận, đếm số từ theo thời gian thực (Live Word Count), thử bấm `Ctrl+V` để thấy hệ thống khóa lệnh Paste từ clipboard.
     * **4. Speaking:** Giao diện **giám khảo ảo hội thoại nhiều lượt**:
       * Đọc **text gợi ý** (dàn ý + từ vựng + câu mẫu) ở khung xanh, rồi trả lời: bấm **Ghi Âm** hoặc **Nhận Dạng Giọng Nói** (offline) để chuyển lời nói thành text, hoặc gõ tay.
       * Bấm **Gửi Câu Trả Lời** → AI chấm ngay lượt đó (Band + 4 tiêu chí + lỗi ngữ pháp màu đỏ/vàng/xanh) và đưa **câu hỏi tiếp theo**.
       * Luồng tự động: **Part 1** (3 câu) → **Part 2** (cue card, có 60 giây chuẩn bị và nút "Bắt Đầu Nói Ngay") → **Part 3** (3 câu, trong đó có 1 câu hỏi phản biện bám theo nội dung bạn vừa nói).
       * Bấm **Kết Thúc & Chốt Điểm Speaking** để nộp; kết quả chờ giảng viên duyệt.
       * *Lưu ý trung thực:* phần chấm dùng bộ chấm mô phỏng rule-based (chưa phân tích waveform âm thanh thật).
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
   - `POST /api/auth/login`: Đăng nhập, trả về **JWT thật** (HS256). Bấm nút **Authorize** trên Swagger và dán token để gọi các API cần đăng nhập.
   - `GET /api/exams`, `GET /api/exams/{id}`: Danh sách / chi tiết đề thi (đã ẩn `CorrectAnswer`).
   - `POST /api/examsessions/start`: Khởi tạo ca thi mới cho thí sinh.
   - `POST /api/examsessions/autosave`: Lưu bản nháp xuống bảng `ExamDrafts`.
   - `GET /api/examsessions/{id}/draft?skillType=3`: Khôi phục bản nháp đã lưu.
   - `POST /api/examsessions/submit-skill`: Nộp bài từng kỹ năng và kích hoạt Strategy Pattern chấm điểm.
   - `GET /api/examsessions/{id}/result`: Báo cáo kết quả, Overall Band của ca thi.
   - `GET /api/grading/pending-reviews`: Bài thi chờ Giảng viên duyệt (yêu cầu role Teacher/Admin).
   - `POST /api/grading/approve-score`: Giảng viên điều chỉnh 4 tiêu chí và chốt điểm chính thức (tự tính lại Overall Band).
   - `POST /api/proctoring/sessions/{id}/terminate`: Giám thị **đình chỉ ca thi** (chuyển trạng thái `Terminated`, ghi biên bản, thí sinh không nộp bài được nữa).
   - `GET|POST|PUT|DELETE /api/admin/exams…`: Quản trị đề thi đầy đủ — đề thi, phần thi (4 kỹ năng), câu hỏi (yêu cầu role **Admin**).
   - `GET /api/speaking/{sessionId}/prompts`: Kịch bản vấn đáp Speaking (câu hỏi + text gợi ý cho thí sinh đọc).
   - `POST /api/speaking/turns`: Nộp 1 câu trả lời → AI chấm lượt đó và trả về câu hỏi tiếp theo (adaptive follow-up).
   - `POST /api/speaking/finalize`: Chốt điểm toàn bộ buổi vấn đáp và lưu bài nộp.

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

### Chạy Unit Test:
```powershell
dotnet test tests/SmartIELTS.UnitTests/SmartIELTS.UnitTests.csproj
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
*(Nếu muốn chuyển sang chạy SQLite offline mang đi nộp máy khác không có SQL Server, chỉ cần sửa `"DatabaseProvider": "Sqlite"`.)*

### Ghi chú kỹ thuật về CSDL

- Schema được khởi tạo bằng `EnsureCreated()`; khi bổ sung bảng mới (ví dụ `ExamDrafts`), `SmartIeltsSchemaGuard` tự thêm vào database **đang tồn tại** nên không phải xóa DB làm lại.
- Đồ án **giữ dual provider** (SQL Server mặc định, SQLite dự phòng) theo yêu cầu môn học. Phiên bản production nên chuyển sang EF Core Migrations; hiện tại chưa làm vì mỗi provider cần một bộ migration riêng.
- Mật khẩu được băm PBKDF2-SHA256. Dữ liệu seed cũ dạng plaintext sẽ **tự động nâng cấp** ngay lần đăng nhập đầu tiên.

---

## 5. Kịch Bản Trình Diễn Khi Chấm Bài (15 Phút)
1. **Phút 0 - 3 (Kiến trúc SWD392):** Mở Visual Studio giới thiệu cấu trúc Clean Architecture phân tầng, chỉ ra nơi triển khai Strategy Pattern cho 4 kỹ năng chấm thi tại `SmartIELTS.Application/GradingStrategies/`, và tầng Use Case dùng chung cho Web API + WPF tại `SmartIELTS.Infrastructure/Services/`.
2. **Phút 3 - 6 (Bảo mật & Kiosk PRN222):** Chạy `SmartIELTS.WpfApp`, bấm Alt+Tab và phím Windows trực tiếp trước mặt hội đồng để chứng minh hệ thống đã khóa cứng phím tắt bằng Win32 Low-Level Keyboard Hook. Thử Paste văn bản vào Writing để minh chứng chống gian lận.
3. **Phút 6 - 12 (Trải nghiệm bài thi 4 kỹ năng):** Thao tác qua 4 phần thi Listening (phát audio), Reading chia đôi màn hình, Writing đếm từ trực tiếp, và Speaking **ghi âm bằng microphone** rồi nghe lại.
   * *Mẹo demo realtime:* mở **2 cửa sổ** — một đăng nhập `student` đang thi, một đăng nhập `teacher` đang ở tab Giám sát. Bấm `Alt+Tab` ở cửa sổ thí sinh: cảnh báo vi phạm sẽ **bật lên ngay trên dashboard giáo viên** (Badge "Realtime: Đã kết nối"). Nếu Web API không chạy, badge hiện "Ngoại tuyến" nhưng cảnh báo cục bộ vẫn hoạt động.
4. **Phút 12 - 15 (Duyệt điểm Human-in-the-loop & Báo cáo):** Xem bản Báo cáo chẩn đoán Band Score, bài mẫu Band 8.0+ do AI viết lại và câu hỏi xoáy Part 3.
   * Điểm nhấn kỹ thuật nên nói: điểm chốt của giáo viên được **tính lại từ 4 tiêu chí** ở phía server (không tin số client gửi), và **Overall Band của ca thi được cập nhật lại** ngay sau khi duyệt.
   * Nếu còn thời gian, đăng nhập `admin` để demo tab **Quản Trị Đề Thi**: tạo đề → thêm phần thi → thêm câu hỏi, và thử xóa đề đã có ca thi (hệ thống chặn bằng 409 để bảo toàn dữ liệu điểm).
   * Demo thêm **Đình Chỉ Ca Thi**: chọn một ca đang thi → bấm ⛔ Đình Chỉ → quay lại cửa sổ thí sinh thử nộp bài (bị chặn).

## 6. Ghi Chú Kỹ Thuật Nên Nắm Khi Bị Hỏi

| Câu hỏi có thể gặp | Trả lời ngắn |
| :--- | :--- |
| Vì sao WPF không gọi Web API? | Kiến trúc **thick client / offline-first**: phòng thi phải chạy được khi mất mạng. Web API phục vụ tích hợp và giám sát từ xa. |
| Làm sao tránh lặp code giữa API và WPF? | Cả hai cùng gọi `IExamWorkflowService` / `ITeachingDashboardService` / `IExamManagementService` trong tầng Infrastructure. |
| Vì sao đề seed chỉ 5 câu/section mà vẫn ra Band đúng? | `BandScoreCalculator.ScaleToFullTest` quy đổi tỷ lệ về thang 40 câu trước khi tra bảng band. |
| Bảo mật mật khẩu? | PBKDF2-SHA256, 100.000 vòng, salt ngẫu nhiên; dữ liệu plaintext cũ tự nâng cấp khi đăng nhập. |
| Phân quyền? | JWT HS256 + `[Authorize(Roles=...)]`: Admin cho quản trị đề, Teacher/Admin cho duyệt điểm & giám thị. |
| AI ở đâu? | Adapter Pattern với implementation **rule-based mô phỏng**; thay implementation khác không phải sửa tầng Application. |
| Speaking "giao tiếp" thế nào? | `ISpeakingSessionService` giữ trạng thái hội thoại: chấm từng lượt, sinh câu hỏi tiếp theo dựa trên từ khóa trong câu trả lời (education/work/health/environment…) → cảm giác giám khảo thật đang hỏi xoáy. |
| Nhận dạng giọng nói dùng gì? | Windows Speech Recognition (`System.Speech`, `DictationGrammar`) — **offline hoàn toàn**, không cần API key; có thêm grammar cụm IELTS để tăng độ chính xác. |
| Vì sao không cho xóa đề đã có ca thi? | Bảo toàn toàn vẹn điểm đã chấm — trả `409 Conflict`, khuyến nghị `IsActive = false`. |
