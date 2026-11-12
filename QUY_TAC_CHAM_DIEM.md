# QUY TẮC CHẤM ĐIỂM: AI hỗ trợ, GIẢNG VIÊN quyết định

> Tài liệu này mô tả chính xác ai được thấy gì trong hệ thống.
> Đây là yêu cầu nghiệp vụ bắt buộc — đã được kiểm chứng bằng 14 test end-to-end.

---

## 1. Quy tắc cốt lõi

> **AI chỉ hỗ trợ giảng viên chấm bài. Thí sinh KHÔNG BAO GIỜ thấy điểm hay nhận xét của AI.**
> **Điểm chính thức chỉ do giảng viên xác nhận.**

Hệ quả:

| Đối tượng | Thấy điểm AI? | Thấy nhận xét AI? | Thấy điểm chính thức? |
|---|---|---|---|
| **Thí sinh** | ❌ Không | ❌ Không | ✅ Chỉ sau khi giảng viên duyệt |
| **Giảng viên** | ✅ Có (để thẩm định) | ✅ Có (đầy đủ + JSON) | ✅ Có |
| **Admin** | ✅ Có | ✅ Có | ✅ Có |

---

## 2. Phân biệt theo kỹ năng

### Listening & Reading — bài khách quan

Chấm máy xong là **chốt luôn**, thí sinh thấy ngay:

```
Nộp Listening → hiện "Số câu đúng: 5/5, Band 9.0"
```

Lý do: đáp án cố định, không có yếu tố chủ quan. Giống thi trắc nghiệm thật.

### Writing & Speaking — bài chủ quan

Nộp xong thí sinh **chỉ nhận xác nhận đã nộp**, không thấy điểm:

```
Nộp Writing → "Đã nộp bài!
               Bài đã được nộp. Giảng viên sẽ chấm và xác nhận
               điểm chính thức cho bạn."
```

AI chấm **phía sau** và lưu vào database. Giảng viên vào dashboard xem:
- Điểm AI đề xuất (4 tiêu chí + Overall)
- Nhận xét chi tiết của AI
- Danh sách lỗi gạch màu (đỏ/vàng/xanh)
- Bài mẫu Band 8+ do AI viết

Sau đó giảng viên **điều chỉnh 4 slider** và bấm "Phê duyệt". Lúc này thí sinh mới thấy điểm — và đó là điểm của **giảng viên**, không phải điểm AI.

---

## 3. Điều gì bị ẩn khỏi thí sinh

Khi thí sinh gọi API hoặc xem màn hình kết quả:

| Dữ liệu | Trạng thái chưa duyệt | Trạng thái đã duyệt |
|---|---|---|
| `BandScore` của Writing/Speaking | `null` | ✅ Có (điểm giảng viên chốt) |
| `RawScore` | `null` | ✅ Có |
| `FeedbackComments` (nhận xét AI) | `""` | `""` — **vẫn ẩn** |
| `DetailedJsonAnalysis` (lỗi chi tiết, bài mẫu) | `""` | `""` — **vẫn ẩn** |
| `CriterionScore1..4` | Không có report | ✅ Có (giảng viên chốt) |
| `TeacherFeedback` | `null` | ✅ Có |
| `OverallBandScore` | `null` | Chỉ khi **cả 4 kỹ năng** đã duyệt |

Thí sinh thấy thông điệp trạng thái rõ ràng:

```
"Bài làm đã được ghi nhận. Phần Writing đang chờ giảng viên chấm
 và xác nhận điểm. Điểm chính thức chỉ hiển thị sau khi giảng viên duyệt."
```

---

## 4. Bảo mật: chống xem điểm người khác

`GET /api/examsessions/{id}/result` kiểm tra quyền sở hữu:

```csharp
// Thí sinh chỉ xem được ca thi CỦA MÌNH
if (!isStaff)
{
    string? currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var owner = await _workflow.GetSessionOwnerIdAsync(id, ct);
    if (owner != userId) return Forbid();   // 403
}
```

- Thí sinh A biết GUID session của thí sinh B → **403 Forbidden**
- Giảng viên/Admin → xem được tất cả (nhận bản DTO đầy đủ)

---

## 5. Vị trí code (khi cần giải thích/bảo vệ)

| Thành phần | File | Vai trò |
|---|---|---|
| **Lọc dữ liệu cho thí sinh** | `Infrastructure/Services/DtoMapper.cs` | `ToSessionResultDto()` — ẩn toàn bộ dữ liệu AI |
| **Bản đầy đủ cho giảng viên** | `DtoMapper.ToSessionResultDtoForStaff()` | Trả điểm + nhận xét AI |
| **Không trả điểm khi nộp bài** | `ExamWorkflowService.SubmitSkillAsync()` | `ShowsRawScore` / `IsPendingReview` |
| **Speaking ẩn điểm từng lượt** | `SpeakingSessionService.EvaluateTurnAsync()` | Chỉ trả câu hỏi tiếp theo + gợi ý |
| **Lưu nhận xét giảng viên riêng** | `TeachingDashboardService.ApproveScoreAsync()` | Ghi `TeacherFeedback`, `ApprovedAt`, `ApprovedBy` |
| **Chặn xem điểm người khác** | `ExamSessionsController.GetSessionResult()` | So khớp `ClaimTypes.NameIdentifier` |

---

## 6. Kết quả kiểm chứng (14 test end-to-end)

```
=== 3. NỘP LISTENING (khách quan) ===
   ShowsRawScore=True  Band=9  Raw=5/5
   [PASS] Listening: thí sinh PHẢI thấy điểm

=== 4. NỘP WRITING (chủ quan) ===
   ShowsRawScore=False  Band=0  Pending=True
   AI Feedback = ''
   Status: Bài đã được nộp. Giảng viên sẽ chấm và xác nhận...
   [PASS] Writing: thí sinh KHÔNG thấy điểm AI
   [PASS] Writing: thí sinh KHÔNG thấy nhận xét AI
   [PASS] Writing: báo đúng là đang chờ duyệt

=== 5. THÍ SINH XEM KẾT QUẢ ===
   Overall = null  FullyApproved=False  Pending=1
   - Listening  band=9.0  DA DUYET  reports=1
   - Writing    band=ẨN   CHO DUYET  reports=0
   [PASS] Writing: thí sinh không thấy band
   [PASS] Writing: không lộ report
   [PASS] Overall bị ẩn khi chưa duyệt

=== 6. GIẢNG VIÊN XEM KẾT QUẢ ===
   Writing band=6  reports=1
   AI feedback: Bài viết đạt 29 từ. Đạt Band 6...
   DetailedJson: 1379 ký tự
   [PASS] Giảng viên PHẢI thấy điểm AI
   [PASS] Giảng viên PHẢI thấy nhận xét AI

=== 7. GIẢNG VIÊN DUYỆT ĐIỂM ===
   Band chốt=7.0
   [PASS] Duyệt thành công

=== 8. THÍ SINH XEM LẠI SAU KHI DUYỆT ===
   Writing band=7.0  Approved=True
   TeacherFeedback=Bài viết tốt, lập luận rõ ràng...
   [PASS] Sau duyệt: thí sinh THẤY điểm
   [PASS] Sau duyệt: thấy nhận xét GIẢNG VIÊN
   [PASS] Sau duyệt: VẪN không lộ nhận xét AI
   [PASS] Sau duyệt: VẪN không lộ DetailedJson

=======================================================
KẾT QUẢ: 14 PASS / 0 FAIL
=======================================================
```

---

## 7. Kịch bản demo gợi ý

Để chứng minh quy tắc này trước hội đồng:

1. **Đăng nhập `student`** → làm Writing → nộp
   - ✅ Chỉ thấy "Giảng viên sẽ chấm và xác nhận điểm"
   - ❌ Không thấy Band, không thấy nhận xét

2. **Mở cửa sổ thứ 2, đăng nhập `teacher`** → tab "Duyệt điểm"
   - ✅ Thấy đầy đủ: điểm AI đề xuất, nhận xét chi tiết, lỗi gạch màu, bài mẫu
   - Điều chỉnh 4 slider → bấm "Phê duyệt"

3. **Quay lại cửa sổ `student`** → mở lại kết quả
   - ✅ Thấy điểm (do giảng viên chốt)
   - ✅ Thấy nhận xét của giảng viên
   - ❌ Vẫn không thấy nhận xét AI

**Câu nói khi bảo vệ:** *"AI đóng vai trò trợ lý chấm bài — nó đề xuất điểm và phân tích lỗi để giảng viên tham khảo. Quyết định cuối cùng luôn thuộc về giảng viên, và thí sinh chỉ nhận điểm do giảng viên xác nhận."*
