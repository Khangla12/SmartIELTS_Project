# HỆ THỐNG KHẢO THÍ TIẾNG ANH TOÀN DIỆN IELTS 4 KỸ NĂNG THÔNG MINH (SmartIELTS)
> **Dự án liên môn:** SWD392 (Software Architecture & Design) & PRN222 (.NET & WPF Desktop Application)  
> **Nền tảng:** .NET 8 (C#) • Clean Architecture • WPF Kiosk Mode • ASP.NET Core Web API • SignalR • Entity Framework Core

---

> [!TIP]
> **HƯỚNG DẪN KHỞI CHẠY CHI TIẾT:**  
> Toàn bộ hướng dẫn mở Solution, cách chạy trên Visual Studio, tài khoản đăng nhập mẫu và kịch bản demo đã được chuyển sang tài liệu riêng:  
> 👉 **[HUONG_DAN_CHAY_DU_AN.md](file:///c:/FPT/7/SWD392/SmartIELTS_Project/HUONG_DAN_CHAY_DU_AN.md)**

---

## 1. Cấu Trúc Solution & Phân Tầng Clean Architecture

Hệ thống được tổ chức hoàn chỉnh theo mô hình **Clean Architecture (Onion Architecture)** tại Solution [`SmartIELTS.sln`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/SmartIELTS.sln) (và [`SmartIELTS.slnx`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/SmartIELTS.slnx)):

```
SWD392/
├── SmartIELTS.sln                     # File Solution mở bằng Visual Studio 2022/2025
├── SmartIELTS.slnx                    # File Solution định dạng XML hiện đại (.NET 10/VS vNext)
├── HUONG_DAN_CHAY_DU_AN.md       # Hướng dẫn chi tiết chạy dự án và danh sách tài khoản
├── README.md                     # Tài liệu tổng quan kiến trúc và thiết kế hệ thống
├── src/
│   ├── SmartIELTS.Domain/             # Tầng Domain độc lập (Entities, Enums, Interfaces, BandScoreCalculator)
│   ├── SmartIELTS.Application/        # Tầng Application (DTOs, Strategy Pattern chấm 4 kỹ năng, Factory)
│   ├── SmartIELTS.Infrastructure/     # Tầng Hạ tầng (SmartIeltsDbContext, Repositories, AI & Speech Adapters, Seeder)
│   ├── SmartIELTS.WebApi/             # Presentation Web API (RESTful Endpoints, SignalR Hubs, Swagger UI)
│   └── SmartIELTS.WpfApp/             # Máy trạm thi Kiosk Mode (Low-Level Keyboard Hook, Split-Screen, Offline SQLite)
└── Slide/ & Docs/                # Slide lý thuyết SWD392, SRS, Sơ đồ Use Case, Luồng thi 10 bước
```

---

## 2. Thiết Kế Cơ Sở Dữ Liệu (Database Schema 3NF)

Cơ sở dữ liệu được thiết kế chuẩn hóa 3NF trên Entity Framework Core gồm 8 thực thể cốt lõi:

1. **`Users`**: Quản lý thông tin thí sinh, giảng viên và quản trị viên (`Id`, `Username`, `PasswordHash`, `FullName`, `Email`, `Role`).
2. **`Exams`**: Thông tin kỳ thi IELTS (`Id`, `Title`, `Description`, `ExamType`, `TotalDurationMinutes`, `IsActive`).
3. **`ExamSections`**: 4 phân hệ kỹ năng của đề thi (`Id`, `ExamId`, `SkillType`, `SectionOrder`, `DurationMinutes`, `MediaUrl`, `PassageText`).
4. **`Questions`**: Danh sách câu hỏi thi (`Id`, `SectionId`, `QuestionNumber`, `QuestionType`, `QuestionText`, `OptionsJson`, `CorrectAnswer`, `MaxScore`).
5. **`ExamSessions`**: Ca thi của từng thí sinh (`Id`, `UserId`, `ExamId`, `StartTime`, `EndTime`, `Status`, `OverallBandScore`).
6. **`CandidateSubmissions`**: Bài nộp từng kỹ năng (`Id`, `SessionId`, `SkillType`, `SubmittedAt`, `RawScore`, `BandScore`, `AudioRecordUrl`, `EssayContent`, `AnswersJson`).
7. **`EvaluationReports`**: Bản đánh giá chi tiết của AI và Giảng viên (`Id`, `SubmissionId`, `EvaluatedBy`, `CriterionScore1`..`4`, `FeedbackComments`, `DetailedJsonAnalysis`, `IsApproved`).
8. **`ProctoringLogs`**: Nhật ký giám thị và cảnh báo vi phạm Kiosk (`Id`, `SessionId`, `Timestamp`, `ViolationType`, `EvidenceSnapshotUrl`, `Description`).

---

## 3. Các Design Pattern & Thuộc Tính Chất Lượng Đã Triển Khai (SWD392)

| Design Pattern | Mục đích ứng dụng trong SmartIELTS | File mã nguồn triển khai |
| :--- | :--- | :--- |
| **Strategy Pattern** | Đa hình hóa cơ chế chấm thi 4 kỹ năng độc lập | [`IExamGrader.cs`](file:///c:/FPT/7/SWD392/src/SmartIELTS.Application/GradingStrategies/IExamGrader.cs), [`ListeningToleranceGrader.cs`](file:///c:/FPT/7/SWD392/src/SmartIELTS.Application/GradingStrategies/ListeningToleranceGrader.cs), [`ReadingExactGrader.cs`](file:///c:/FPT/7/SWD392/src/SmartIELTS.Application/GradingStrategies/ReadingExactGrader.cs), [`AiGraderStrategies.cs`](file:///c:/FPT/7/SWD392/src/SmartIELTS.Application/GradingStrategies/AiGraderStrategies.cs) |
| **Factory Method** | Cấp phát động bộ chấm thi phù hợp với từng SkillType | [`GradingStrategyFactory`](file:///c:/FPT/7/SWD392/src/SmartIELTS.Application/GradingStrategies/IExamGrader.cs) |
| **Adapter Pattern** | Bọc các dịch vụ AI bên ngoài (LLM, Speech Assessment) đằng sau Interface chuẩn của hệ thống | [`IAiServiceAdapter.cs`](file:///c:/FPT/7/SWD392/src/SmartIELTS.Application/Interfaces/IAiServiceAdapter.cs), [`IntelligentAiServiceAdapter.cs`](file:///c:/FPT/7/SWD392/src/SmartIELTS.Infrastructure/Adapters/IntelligentAiServiceAdapter.cs) |
| **Observer / Pub-Sub** | Đồng bộ thời gian thi và bắn cảnh báo vi phạm Kiosk thời gian thực | [`ExamHub.cs`](file:///c:/FPT/7/SWD392/src/SmartIELTS.WebApi/Hubs/ExamHub.cs), [`ProctoringHub.cs`](file:///c:/FPT/7/SWD392/src/SmartIELTS.WebApi/Hubs/ExamHub.cs) |
| **Unit of Work & Repository** | Quản trị giao dịch dữ liệu chuẩn ACID khi lưu trữ nhiều section bài thi | [`SmartIeltsRepository.cs`](file:///c:/FPT/7/SWD392/src/SmartIELTS.Infrastructure/Repositories/SmartIeltsRepository.cs) |

---

## 4. Kỹ Thuật Bảo Mật & Kiosk Mode (PRN222)

- **Low-Level Windows Keyboard Hook:** Triển khai tại [`WindowsKeyboardHook.cs`](file:///c:/FPT/7/SWD392/src/SmartIELTS.WpfApp/Services/WindowsKeyboardHook.cs) bằng `SetWindowsHookEx(WH_KEYBOARD_LL = 13)` qua Win32 P/Invoke. Chặn triệt để các phím tắt: `Alt+Tab`, `Windows Key`, `Alt+Esc`, `Ctrl+Esc`, `PrintScreen`, `Alt+F4`.
- **Khóa Lệnh Dán Nội Dung (Clipboard Anti-Paste):** Vô hiệu hóa phím tắt `Ctrl+V` trong trình soạn bài viết Task 1 & 2 để ngăn chặn sao chép văn bản bên ngoài.
- **Khả Năng Chịu Lỗi Ngoại Tuyến (Offline-First):** Cơ chế tự động lưu nháp mỗi 10 giây xuống cơ sở dữ liệu SQLite cục bộ, bảo đảm dữ liệu bài làm được bảo toàn ngay cả khi gặp sự cố ngắt kết nối.
