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
└── Slide/ & Docs/                # Slide lý thuyết SWD392, SRS, Sơ đồ Use Case, Luồng thi 10 bước (nếu có)
```

> **Lưu ý về mã nguồn:** Luồng nghiệp vụ chính (bắt đầu ca thi, chấm điểm, duyệt điểm, autosave)
> được cài đặt **một lần** trong tầng Application/Infrastructure
> ([`ExamWorkflowService`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/src/SmartIELTS.Infrastructure/Services/ExamWorkflowService.cs),
> [`TeachingDashboardService`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/src/SmartIELTS.Infrastructure/Services/TeachingDashboardService.cs))
> và được dùng chung bởi cả Web API lẫn WPF Kiosk.

---

## 2. Thiết Kế Cơ Sở Dữ Liệu (Database Schema 3NF)

Cơ sở dữ liệu được thiết kế chuẩn hóa 3NF trên Entity Framework Core gồm 8 thực thể cốt lõi (và bảng phụ `ExamDrafts` cho autosave):

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
| **Strategy Pattern** | Đa hình hóa cơ chế chấm thi 4 kỹ năng độc lập. Dùng chung cho Web API và WPF qua `IExamWorkflowService` | [`IExamGrader.cs`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/src/SmartIELTS.Application/GradingStrategies/IExamGrader.cs), [`ListeningToleranceGrader.cs`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/src/SmartIELTS.Application/GradingStrategies/ListeningToleranceGrader.cs), [`ReadingExactGrader.cs`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/src/SmartIELTS.Application/GradingStrategies/ReadingExactGrader.cs), [`AiGraderStrategies.cs`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/src/SmartIELTS.Application/GradingStrategies/AiGraderStrategies.cs) |
| **Factory Method** | Cấp phát động bộ chấm thi phù hợp với từng SkillType | [`GradingStrategyFactory`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/src/SmartIELTS.Application/GradingStrategies/IExamGrader.cs) |
| **Adapter Pattern** | Bọc dịch vụ AI bên ngoài (LLM, Speech Assessment) đằng sau Interface chuẩn của hệ thống. Hiện `IntelligentAiServiceAdapter` là bộ chấm **mô phỏng rule-based** (chưa gọi dịch vụ AI thật) và có thể thay thế bằng implementation khác mà không phải sửa tầng Application | [`IAiServiceAdapter.cs`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/src/SmartIELTS.Application/Interfaces/IAiServiceAdapter.cs), [`IntelligentAiServiceAdapter.cs`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/src/SmartIELTS.Infrastructure/Adapters/IntelligentAiServiceAdapter.cs) |
| **Stateful Conversation (Speaking)** | Giám khảo ảo hội thoại nhiều lượt: chấm từng câu trả lời, sinh câu hỏi tiếp theo bám nội dung vừa nói (adaptive follow-up), chuyển Part 1 → 2 → 3 tự động | [`SpeakingSessionService.cs`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/src/SmartIELTS.Infrastructure/Services/SpeakingSessionService.cs), [`SpeakingPrompt.cs`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/src/SmartIELTS.Domain/Entities/SpeakingPrompt.cs) |
| **Observer / Pub-Sub** | Cảnh báo vi phạm Kiosk: WPF phát qua `IProctoringNotificationBus` (hiện ngay cả khi offline), Web API cầu nối sang SignalR cho dashboard ở máy khác | [`ExamHub.cs`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/src/SmartIELTS.WebApi/Hubs/ExamHub.cs), [`ProctoringSignalRBridge.cs`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/src/SmartIELTS.WebApi/Realtime/ProctoringSignalRBridge.cs), [`IProctoringNotificationBus.cs`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/src/SmartIELTS.Application/Interfaces/IProctoringNotificationBus.cs) |
| **Unit of Work & Repository** | Quản trị giao dịch dữ liệu chuẩn ACID khi lưu trữ nhiều section bài thi | [`SmartIeltsRepository.cs`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/src/SmartIELTS.Infrastructure/Repositories/SmartIeltsRepository.cs) |
| **Clean Architecture Use Case** | Một cài đặt nghiệp vụ dùng chung cho Web API + WPF (không lặp code): ca thi, chấm điểm, duyệt điểm, quản trị đề thi | [`ExamWorkflowService.cs`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/src/SmartIELTS.Infrastructure/Services/ExamWorkflowService.cs), [`TeachingDashboardService.cs`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/src/SmartIELTS.Infrastructure/Services/TeachingDashboardService.cs), [`ExamManagementService.cs`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/src/SmartIELTS.Infrastructure/Services/ExamManagementService.cs) |

---

## 4. Kỹ Thuật Bảo Mật & Kiosk Mode (PRN222)

- **Low-Level Windows Keyboard Hook:** Triển khai tại [`WindowsKeyboardHook.cs`](file:///c:/FPT/7/SWD392/SmartIELTS_Project/src/SmartIELTS.WpfApp/Services/WindowsKeyboardHook.cs) bằng `SetWindowsHookEx(WH_KEYBOARD_LL = 13)` qua Win32 P/Invoke. Chặn triệt để các phím tắt: `Alt+Tab`, `Windows Key`, `Alt+Esc`, `Ctrl+Esc`, `PrintScreen`, `Alt+F4`.
- **Khóa Lệnh Dán Nội Dung (Clipboard Anti-Paste):** Vô hiệu hóa phím tắt `Ctrl+V` trong trình soạn bài viết Task 1 & 2 để ngăn chặn sao chép văn bản bên ngoài.
- **Khả Năng Chịu Lỗi Ngoại Tuyến (Offline-First):** Cơ chế tự động lưu nháp mỗi 10 giây xuống bảng `ExamDrafts` của database đang cấu hình (SQLite hoặc SQL Server), có thể khôi phục lại khi chuyển kỹ năng hoặc mở lại ứng dụng.
- **Ghi Âm Speaking (NAudio):** Thu âm câu trả lời từ microphone ra file WAV 16kHz mono, nghe lại được trước khi nộp. Database chỉ lưu **đường dẫn tương đối** để dùng chung CSDL giữa nhiều máy trạm.
  - Lưu ý trung thực về phạm vi: phần **chấm điểm** Speaking vẫn dựa trên nội dung transcript bằng bộ chấm mô phỏng rule-based, chưa phân tích âm thanh thật.
- **Giám Khảo Ảo Speaking (hội thoại nhiều lượt):** Thí sinh đọc **text gợi ý** (dàn ý + từ vựng + câu mẫu) rồi trả lời từng câu; AI chấm ngay lượt đó (4 tiêu chí + lỗi ngữ pháp có màu) và **sinh câu hỏi tiếp theo bám theo nội dung vừa nói**. Luồng chuẩn IELTS: Part 1 (3 câu) → Part 2 (cue card, 1 phút chuẩn bị) → Part 3 (3 câu phản biện).
- **Nhận Dạng Giọng Nói Offline:** Dùng Windows Speech Recognition (System.Speech) để chuyển lời nói thành transcript — không cần API key, không cần internet. Thí sinh vẫn sửa được text trước khi gửi; máy không có gói en-US sẽ tự báo và cho gõ tay.
- **Cảnh Báo Giám Thị Realtime:** Dashboard giáo viên nhận cảnh báo vi phạm qua 2 đường — Observer bus trong cùng tiến trình (luôn hoạt động, kể cả khi Web API tắt) và SignalR khi API đang chạy.
