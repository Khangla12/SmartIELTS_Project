# PROMPT: Thiết kế lại UI/UX SmartIELTS WPF (Dark → Light)

> Copy toàn bộ nội dung từ dòng 12 trở xuống vào session mới.

---

## 1. Bối cảnh

Bạn đang sửa một ứng dụng **WPF .NET 8** tên **SmartIELTS** — hệ thống khảo thí IELTS 4 kỹ năng (đồ án SWD392 + PRN222). Ứng dụng chạy **Kiosk Mode** toàn màn hình cho thí sinh làm bài, và có dashboard cho giảng viên + admin.

**Vấn đề:** giao diện hiện tại dùng dark theme (`#0F172A`) trông nặng nề, xấu, nhiều màu hard-code rải rác, thiếu phân cấp thị giác. Cần chuyển sang **light theme sạch sẽ, dễ đọc, chuyên nghiệp**.

## 2. File cần sửa

| File | Vai trò | Kích thước |
|---|---|---|
| `src/SmartIELTS.WpfApp/MainWindow.xaml` | Toàn bộ UI (11 màn hình) | 846 dòng |
| `src/SmartIELTS.WpfApp/MainWindow.xaml.cs` | Code-behind | 1680 dòng — **hạn chế sửa** |
| `src/SmartIELTS.WpfApp/Dialogs/FormDialog.cs` | Dialog nhập liệu | được phép sửa |

## 3. Ràng buộc CỨNG (không được vi phạm)

Đây là phần quan trọng nhất. Code-behind tham chiếu trực tiếp tới tên control và resource key, nên **đổi tên = app crash ngay**.

### 3.1. Giữ nguyên tuyệt đối 108 `x:Name`
Không đổi tên, không xóa, không thêm control mới có `x:Name`. Danh sách đầy đủ:

```
BadgeKiosk, BadgeRealtime, BarAudioProgress, BarSpeakingProgress, BtnAdminAddQuestion,
BtnAdminAddSection, BtnAdminDeleteQuestion, BtnAdminDeleteSection, BtnDashTabExams,
BtnDashTabGrading, BtnDashTabProctoring, BtnFinalizeSpeaking, BtnLogout, BtnPlayAudio,
BtnPlayRecording, BtnRefreshDashboard, BtnReplayQuestion, BtnSkipPreparation,
BtnStartRecording, BtnStartSpeechToText, BtnStopRecording, BtnSubmitCurrentSkill,
BtnSubmitTurn, BtnTerminateSession, BtnToggleKiosk, BtnToggleVoice, CardTurnFeedback,
ListAdminExams, ListAdminQuestions, ListAdminSections, ListPendingReviews, ListSessions,
ListSpeakingTurns, ListTurnHighlights, ListViolations, PanelDashGrading, PanelDashProctoring,
PanelListening, PanelReading, PanelSpeaking, PanelWriting, SliderCC, SliderGRA, SliderLR,
SliderTR, StackListeningQuestions, StackReadingQuestions, TabListening, TabReading,
TabSpeaking, TabWriting, TxtActionStatus, TxtAdaptiveQuestion, TxtAdminExamTitle,
TxtAudioStatus, TxtCandidateBadge, TxtCandidateGuide, TxtDiagnosticFeedback, TxtEssayEditor,
TxtExaminerQuestion, TxtKioskStatus, TxtKpiAvgBand, TxtKpiPending, TxtKpiSessions,
TxtKpiViolations, TxtManualViolationDesc, TxtModelAnswer, TxtOfficialBandScore,
TxtOverallBand, TxtPassword, TxtProctoringAlert, TxtReadingPassage, TxtRealtimeStatus,
TxtRecordingStatus, TxtReviewAiFeedback, TxtReviewAiProposedBadge, TxtReviewCandidateTitle,
TxtReviewEssayContent, TxtScoreListening, TxtScoreReading, TxtScoreSpeaking, TxtScoreWriting,
TxtSelectedSession, TxtSpeakingPartBadge, TxtSpeakingProgress, TxtSpeakingState,
TxtSpeakingStats, TxtSpeakingTimer, TxtSpeakingTranscript, TxtSpeechStatus,
TxtTeacherFeedback, TxtTimer, TxtTurnBand, TxtTurnCriteria, TxtTurnFeedback, TxtUsername,
TxtValCC, TxtValGRA, TxtValLR, TxtValTR, TxtVolume, TxtWordCount, TxtWritingPrompt,
ViewAdminExams, ViewExam, ViewLogin, ViewResult, ViewTeacherDashboard
```

### 3.2. Giữ nguyên 42 tên event handler
```
BtnAddManualViolation_Click, BtnAdminAddQuestion_Click, BtnAdminAddSection_Click,
BtnAdminBack_Click, BtnAdminCreateExam_Click, BtnAdminDeleteExam_Click,
BtnAdminDeleteQuestion_Click, BtnAdminDeleteSection_Click, BtnAdminRefresh_Click,
BtnApproveScore_Click, BtnDashTab_Click, BtnExitApp_Click, BtnFillDemoAnswers_Click,
BtnFinalizeSpeaking_Click, BtnHighlight_Click, BtnLogout_Click, BtnPlayAudio_Click,
BtnPlayRecording_Click, BtnQuickRole_Click, BtnRefreshDashboard_Click,
BtnReplayQuestion_Click, BtnRestart_Click, BtnSkipPreparation_Click, BtnStartExam_Click,
BtnStartRecording_Click, BtnStartSpeechToText_Click, BtnStopRecording_Click,
BtnSubmitCurrentSkill_Click, BtnSubmitTurn_Click, BtnTerminateSession_Click,
BtnToggleKiosk_Click, BtnToggleVoice_Click, CriteriaSlider_ValueChanged,
ListAdminExams_SelectionChanged, ListAdminQuestions_SelectionChanged,
ListAdminSections_SelectionChanged, ListPendingReviews_SelectionChanged,
ListSessions_SelectionChanged, TabSkill_Click, TxtEssayEditor_PreviewKeyDown,
TxtEssayEditor_TextChanged, Window_Closing
```

### 3.3. Giữ nguyên 6 tên resource key (code-behind gọi `FindResource`)
Code-behind gọi các key này trong 25 chỗ. **Tên phải giữ, chỉ đổi giá trị màu:**

| Key | Số lần dùng trong code-behind | Màu mới |
|---|---|---|
| `DangerRed` | 8 | `#DC2626` |
| `PrimaryTeal` | 7 | `#0F766E` |
| `SuccessGreen` | 5 | `#0E9F6E` |
| `TextLight` | 2 | `#0F172A` |
| `AccentBlue` | 2 | `#1D4ED8` |
| `WarningYellow` | 1 | `#B45309` |

### 3.4. Cấm thêm mới
- ❌ Không thêm NuGet package
- ❌ Không thêm file XAML/Window mới
- ❌ Không refactor sang MVVM
- ❌ Không đổi logic, không đổi luồng nghiệp vụ
- ❌ Không thêm animation, gradient phức tạp, glassmorphism, icon font, image asset
- ❌ Không thêm màn hình/tính năng mới

### 3.5. Được phép
- ✅ Đổi toàn bộ màu sắc, cỡ chữ, khoảng cách, bo góc
- ✅ Tách style vào `Window.Resources` (giảm hard-code)
- ✅ Đổi layout trong từng panel (Grid/ColumnDefinition) miễn giữ control
- ✅ Thay emoji bằng text hoặc ngược lại (xem mục 6)
- ✅ Sửa `MainWindow.xaml.cs` **chỉ** ở các dòng gán màu literal

## 4. Design system bắt buộc

### 4.1. Bảng màu Light (dùng đúng các mã này)

```xml
<!-- Nền -->
<SolidColorBrush x:Key="BgPage"        Color="#F5F7FA"/>  <!-- nền tổng -->
<SolidColorBrush x:Key="BgSurface"     Color="#FFFFFF"/>  <!-- card -->
<SolidColorBrush x:Key="BgSubtle"      Color="#EEF2F7"/>  <!-- header row, vùng phụ -->
<SolidColorBrush x:Key="BorderSubtle"  Color="#DDE3EA"/>  <!-- viền 1px -->
<SolidColorBrush x:Key="BorderStrong"  Color="#C3CCD8"/>

<!-- Chữ -->
<SolidColorBrush x:Key="TextPrimary"   Color="#0F172A"/>  <!-- tiêu đề -->
<SolidColorBrush x:Key="TextBody"      Color="#334155"/>  <!-- nội dung -->
<SolidColorBrush x:Key="TextMuted"     Color="#6B7A90"/>  <!-- phụ, ghi chú -->

<!-- Thương hiệu & trạng thái -->
<SolidColorBrush x:Key="PrimaryTeal"   Color="#0F766E"/>  <!-- CTA chính -->
<SolidColorBrush x:Key="PrimaryHover"  Color="#0D9488"/>
<SolidColorBrush x:Key="PrimarySoft"   Color="#E6F4F2"/>  <!-- nền badge -->
<SolidColorBrush x:Key="AccentBlue"    Color="#1D4ED8"/>
<SolidColorBrush x:Key="AccentSoft"    Color="#E8EEFE"/>
<SolidColorBrush x:Key="SuccessGreen"  Color="#0E9F6E"/>
<SolidColorBrush x:Key="SuccessSoft"   Color="#E3F5EC"/>
<SolidColorBrush x:Key="WarningYellow" Color="#B45309"/>
<SolidColorBrush x:Key="WarningSoft"   Color="#FDF3E3"/>
<SolidColorBrush x:Key="DangerRed"     Color="#DC2626"/>
<SolidColorBrush x:Key="DangerSoft"    Color="#FDECEA"/>

<!-- Giữ key cũ để không vỡ code-behind -->
<SolidColorBrush x:Key="TextLight"     Color="#0F172A"/>  <!-- BẮT BUỘC giữ tên -->
<SolidColorBrush x:Key="BgDark"        Color="#F5F7FA"/>
<SolidColorBrush x:Key="BgCard"        Color="#FFFFFF"/>
```

### 4.2. Typography
Font: `Segoe UI`. Thang cỡ chữ:

| Vai trò | Size | Weight | Dùng cho |
|---|---|---|---|
| Hero | 40 | Bold | `TxtOverallBand` |
| H1 | 24 | SemiBold | Tiêu đề màn hình |
| H2 | 18 | SemiBold | Tiêu đề khu vực |
| H3 | 15 | SemiBold | Tiêu đề card |
| Body | 14 | Normal | Nội dung chính |
| Small | 12 | Normal | Nhãn, metadata |
| Caption | 11 | Normal | Ghi chú |

**Tối thiểu 12px.** LineHeight = 1.5 × fontSize (ví dụ body 14 → LineHeight 21).

### 4.3. Spacing & Shape
- Bước nhảy: **4 / 8 / 12 / 16 / 24 / 32**
- Bo góc: **6** (badge, button nhỏ) · **10** (card) · **999** (pill)
- Card padding: **16–20**
- Khoảng cách giữa card: **12–16**
- Viền: **1px** `BorderSubtle`. Cấm viền dày >1px (trừ focus ring 2px)
- Đổ bóng: tối đa **1 cấp** `DropShadowEffect BlurRadius=8 Opacity=0.06 ShadowDepth=1`

## 5. Yêu cầu từng màn hình

### 5.1. Header (dòng 75–119)
- Nền trắng, viền dưới 1px, cao 64
- Logo: badge vuông 36×36, nền `PrimaryTeal`, chữ trắng "AI"
- Badge thí sinh: nền `BgSubtle`, chữ `TextBody`, pill
- Badge đồng hồ: nền `AccentSoft`, chữ `AccentBlue`, **số đếm `TxtTimer` size 16 Bold dễ thấy**
- Badge Kiosk: nền `SuccessSoft` + chữ `SuccessGreen`
- Badge Realtime: nền `AccentSoft` + chữ `AccentBlue`

### 5.2. ViewLogin
- Nền `BgPage`, card trắng giữa màn hình, max-width 460, bo 10, padding 32
- Thứ tự: logo → tiêu đề H1 → phụ đề Small → 3 nút chọn vai trò (pill, viền `BorderSubtle` thay vì màu đậm) → `TxtUsername` → `TxtPassword` → nút CTA lớn `PrimaryTeal` cao 44
- Nút chọn vai trò hiện tại đang dùng nền xanh đậm/đỏ/xanh navy rực rỡ → đổi thành nền trắng + viền, hover mới đổi nền

### 5.3. ViewExam — thanh 4 tab kỹ năng
- Hiện là 4 button màu đậm. Đổi thành **segmented control**: container nền `BgSubtle` bo 8, các tab trong suốt; tab active nền trắng + chữ `TextPrimary` + viền; tab inactive chữ `TextMuted`
- ⚠️ Code-behind gán `.Background` trực tiếp cho 4 tab này trong `LoadSkillView` và `TabSkill_Click`. **Giữ nguyên property Background** nhưng đổi giá trị gán trong `.cs` từ `PrimaryTeal`/`#475569` sang `PrimarySoft`/`Transparent` — hoặc chấp nhận nền `PrimaryTeal` cho tab active (vẫn đọc tốt trên nền sáng). Chọn 1 trong 2, ghi rõ đã chọn cách nào.

### 5.4. PanelListening
- Card audio: nền `BgSubtle`, có nút Play `PrimaryTeal`, `BarAudioProgress` màu `PrimaryTeal`
- Danh sách câu hỏi: mỗi câu là card trắng viền `BorderSubtle`, padding 14
- Tiêu đề câu: `TextPrimary` 14 SemiBold, số câu trong badge `PrimarySoft`
- RadioButton/TextBox trả lời: viền `BorderSubtle`, focus ring `PrimaryTeal` 2px

### 5.5. PanelReading (split-screen)
- Hai cột bằng nhau, dải phân cách 1px `BorderSubtle` (không dùng GridSplitter màu xám đậm)
- Trái: passage trên nền `BgSubtle`, chữ `TextBody` 15/26 (dễ đọc — đây là bài đọc dài)
- Toolbar highlight: nút nhỏ viền, icon text
- Phải: danh sách câu hỏi như 5.4

### 5.6. PanelWriting
- Trái 380px: đề bài trong card `BgSubtle`; khối "4 tiêu chí" dùng danh sách có badge số 1–4 thay vì bullet
- Phải: `TxtEssayEditor` nền trắng, viền, **LineHeight 26**, font 15, padding 16
- Footer: `TxtWordCount` bên trái (badge `PrimarySoft`), ghi chú autosave bên phải màu `TextMuted`

### 5.7. PanelSpeaking (3 phần)
- Cột trái: card câu hỏi giám khảo nền `AccentSoft` viền `AccentBlue`; card "Đọc và trả lời" nền `SuccessSoft` viền `SuccessGreen` với chữ `TextBody` (hiện chữ xanh lá nhạt trên nền tối → phải đổi thành chữ đậm trên nền nhạt)
- Đồng hồ đếm ngược `TxtSpeakingTimer`: size 18 Bold màu `WarningYellow`
- Cột phải: lịch sử hội thoại, mỗi lượt là card nhỏ viền nhẹ
- Nút `BtnStartSpeechToText` dùng `AccentBlue`

### 5.8. ViewResult
- `TxtOverallBand`: **hero 40 Bold**, đặt trong vòng tròn nền `PrimarySoft`, chữ `PrimaryTeal` — đây là điểm nhấn mạnh nhất màn hình
- 4 dòng kỹ năng: mỗi dòng có label + Band + mô tả, ngăn cách bằng đường 1px
- Khối "bài mẫu Band 8" trong card `BgSubtle`, chữ `TextBody`, LineHeight 24

### 5.9. ViewTeacherDashboard
- 4 card KPI: nền trắng, viền **trái 4px** theo màu ngữ nghĩa (xanh dương / đỏ / cam / xanh lá), số liệu size 28 Bold, nhãn size 12 màu `TextMuted`
- 2 tab: dùng segmented như 5.3
- Bảng `ListView`/`GridView`: header nền `BgSubtle` chữ `TextMuted` 12 SemiBold; dòng xen kẽ trắng / `#FAFBFC`; dòng chọn nền `PrimarySoft`

### 5.10. ViewAdminExams
- Trái 360px danh sách đề; phải chi tiết
- Mỗi đề trong list: tiêu đề SemiBold + dòng metadata `TextMuted` 11
- Nút xóa dùng style "danger outline": nền trắng, viền `DangerRed`, chữ `DangerRed` (tránh nền đỏ đặc gây nặng)

### 5.11. Footer status bar (dòng cuối)
- Nền `BgSubtle`, cao 36, chữ 12
- `TxtProctoringAlert` là thông báo trạng thái — màu thay đổi runtime nên giữ cách gán `Foreground` trong code-behind, chỉ đảm bảo 3 màu `SuccessGreen` / `DangerRed` / `AccentBlue` đọc tốt trên nền `BgSubtle`

### 5.12. FormDialog.cs
- Đổi nền `#0F172A` → `#FFFFFF`, label `#94A3B8` → `#6B7A90`, TextBox chữ `TextBody`
- Nút Lưu dùng `PrimaryTeal`, Hủy dùng viền xám

## 6. Xử lý emoji (17 dòng hiện có)

Giữ **tối đa 8 chỗ** và chỉ ở trạng thái:
- `🔒 / 🔓` (Kiosk), `⏱` (đồng hồ), `✅ / ⚠️ / ⛔` (kết quả thao tác), `🎙️` (micro)

Xóa emoji trang trí ở tiêu đề card/tab (`👥 🚨 ✍️ 📋 🎓 📖 💬 🗂️ 🛡️ …`) → thay bằng chữ thường. Lý do: trên nền sáng emoji màu rực làm rối mắt.

## 7. QUAN TRỌNG: quét sạch dark-mode sót lại

Đây là lỗi dễ xảy ra nhất khi đổi sáng→tối ngược lại. Có **125 mã màu hard-code** trong XAML.

**Trước khi xong, chạy các lệnh sau và xử lý HẾT kết quả:**

```powershell
# 1. Màu chữ sáng (sẽ vô hình trên nền trắng)
Select-String -Path src/SmartIELTS.WpfApp/MainWindow.xaml -Pattern '#E2E8F0|#CBD5E1|#F8FAFC|#A5B4FC|#67E8F9|#D1FAE5|Foreground="White"'

# 2. Nền tối còn sót
Select-String -Path src/SmartIELTS.WpfApp/MainWindow.xaml -Pattern '#0F172A|#1E293B|#334155|#475569|#1E1B4B|#052E2B|#450A0A|#064E3B|#065F46|#1E3A8A'

# 3. Tương tự trong code-behind
Select-String -Path src/SmartIELTS.WpfApp/MainWindow.xaml.cs -Pattern 'FromRgb\(|#0F172A|#1E293B|#475569'
```

Quy tắc thay:
| Cũ (dark) | Mới (light) |
|---|---|
| `#E2E8F0`, `#CBD5E1`, `#F8FAFC`, `White` (chữ) | `#334155` (TextBody) |
| `#A5B4FC`, `#67E8F9` (chữ nhấn) | `#1D4ED8` (AccentBlue) |
| `#D1FAE5` (chữ trên nền tối) | `#334155` (TextBody) |
| `#0F172A`, `#1E293B` (nền) | `#F5F7FA` / `#FFFFFF` |
| `#334155`, `#475569` (nền phụ) | `#EEF2F7` |
| `#1E1B4B`, `#052E2B`, `#450A0A`, `#064E3B`, `#065F46`, `#1E3A8A` | dùng `*Soft` tương ứng |

Trong `MainWindow.xaml.cs` có **74 dòng gán `Background`/`Foreground` trực tiếp**. Sửa các dòng dùng `Color.FromRgb(...)` theo bảng trên. Ví dụ:
```csharp
// Trước
TabReading.Background = new SolidColorBrush(Color.FromRgb(71, 85, 105));
// Sau — nền xám nhạt cho tab không active
TabReading.Background = new SolidColorBrush(Color.FromRgb(238, 242, 247));
```

## 8. Tiêu chí hoàn thành

Chạy và xác nhận:

```powershell
dotnet build SmartIELTS.sln -v q --nologo      # 0 warning, 0 error
dotnet test tests/SmartIELTS.UnitTests/SmartIELTS.UnitTests.csproj -v q   # 92 passed
```

Checklist thủ công (chạy app, đăng nhập `student` / `Student@123` rồi `teacher` / `Teacher@123`, `admin` / `Admin@123`):

- [ ] Mở app không crash, không có control nào chữ trắng trên nền trắng
- [ ] ViewLogin: đọc rõ toàn bộ chữ, nút CTA nổi bật nhất
- [ ] Vào thi được, 4 tab kỹ năng phân biệt được tab active
- [ ] Listening: bấm Phát Audio có tiếng
- [ ] Reading: 2 cột đọc được, cuộn mượt
- [ ] Writing: gõ được, đếm từ cập nhật, Ctrl+V bị chặn
- [ ] Speaking: nghe được giọng đọc câu hỏi, ghi âm được, text tự hiện
- [ ] Nộp 4 kỹ năng → ViewResult hiển thị Overall Band dễ thấy
- [ ] Teacher: 4 KPI card + 2 tab + bảng danh sách đọc rõ
- [ ] Teacher: chọn ca thi → nút Đình Chỉ bấm được
- [ ] Admin: tab Quản Trị Đề Thi hiện, list đề + câu hỏi đọc rõ
- [ ] Dialog tạo đề / thêm câu hỏi: nền trắng, chữ đậm, không lẫn dark
- [ ] Đóng/mở lại app vẫn đúng theme

## 9. Cách làm việc

1. **Trước khi sửa:** liệt kê danh sách thay đổi dự kiến (style mới + màn hình nào đổi gì), chờ tôi xác nhận
2. Sửa theo thứ tự: (a) `Window.Resources` — palette + style, (b) từng màn hình, (c) quét dark-mode sót, (d) sửa `FormDialog.cs`, (e) sửa các dòng gán màu trong `.cs`
3. **Sau mỗi bước:** build lại, báo lỗi ngay nếu có
4. Không sửa gì ngoài phạm vi mục 2
5. Kết thúc: báo cáo ngắn gồm (i) danh sách thay đổi, (ii) output build + test, (iii) những gì **không** làm được và lý do
