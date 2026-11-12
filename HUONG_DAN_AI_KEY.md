# CẤU HÌNH AI CHẤM ĐIỂM — HƯỚNG DẪN CHO CẢ NHÓM

> Tài liệu này giải thích cách bật AI chấm điểm thật trên máy từng thành viên.
> **API key KHÔNG nằm trong git** — mỗi người tự set trên máy mình.

---

## 1. Vì sao key không nằm trong code?

Repo là **public/git chung**. Nếu commit key vào `appsettings.json`:

- ❌ Key bị lộ vĩnh viễn trong lịch sử git (xoá file không xoá được history)
- ❌ Bot quét GitHub sẽ tìm thấy trong vài phút và có thể lạm dụng
- ❌ Hết quota / bị khoá key thì cả nhóm không demo được
- ❌ Nếu là key trả tiền, có thể bị dùng trộm

Vì vậy code chỉ đọc key từ **biến môi trường**. Trong `appsettings.json` chỉ có:

```json
"Ai": {
  "Grading": {
    "Provider": "Auto",
    "BaseUrl": "https://api-sf1.deploy.com/api/v1",
    "Model": "deepseek-ai/deepseek-v4.1-flash-1017",
    "ApiKey": "",
    "ApiKeyEnvironmentVariable": "SMARTIELTS_AI_KEY",
    "TimeoutSeconds": 180,
    "Temperature": 0.2,
    "FallbackToRuleBased": true
  }
}
```

→ `ApiKey` để **rỗng**, tên biến môi trường là `SMARTIELTS_AI_KEY`.

---

## 2. Mỗi thành viên cần làm gì?

### Bước 1: Lấy key từ trưởng nhóm
Key được **chia sẻ riêng** (Zalo / Messenger / Discord), **không** dán vào git, không dán vào issue, không gửi qua PR.

### Bước 2: Set biến môi trường trên máy mình

**PowerShell — chỉ session hiện tại (tạm thời, an toàn nhất):**
```powershell
$env:SMARTIELTS_AI_KEY = "your-key-here"
```

**PowerShell — vĩnh viễn cho user (tiện, nhưng lưu trong Windows):**
```powershell
[Environment]::SetEnvironmentVariable("SMARTIELTS_AI_KEY", "your-key-here", "User")
```
Sau đó **mở lại terminal** (biến chỉ có hiệu lực ở process mới).

**Command Prompt (cmd):**
```cmd
set SMARTIELTS_AI_KEY=your-key-here
```

**Visual Studio:** Properties → Debug → Environment Variables → thêm `SMARTIELTS_AI_KEY` = key.
*(Lưu ý: cách này ghi vào `.csproj.user` — file này đã bị `.gitignore` chặn.)*

### Bước 3: Kiểm tra
```powershell
.\check_ai_key.ps1
```
Kết quả mong đợi:
```
THANH CONG
  Thoi gian   : 2.06s
  Model       : deepseek-ai/deepseek-v4.1-flash-1017
  Reasoning   : 40 tokens
```

### Bước 4: Chạy app như bình thường
```powershell
dotnet run --project src/SmartIELTS.WpfApp/SmartIELTS.WpfApp.csproj
```

---

## 3. Nếu thành viên KHÔNG set key thì sao?

**App vẫn chạy bình thường.** Tự động dùng bộ chấm rule-based (offline):

| | Có key | Không có key |
|---|---|---|
| Bộ chấm | AI thật (LLM) | Rule-based |
| Dải điểm Writing | 2.0 – 8.5 | 6.0 – 7.0 (hẹp, dễ nhận ra) |
| Nhận xét | Trích dẫn cụm từ cụ thể trong bài | Câu mẫu chung chung |
| Thời gian | 25–35s / bài | < 0.1s |
| Cần mạng | Có | Không |

**Dấu hiệu nhận biết đang dùng rule-based:** feedback có chữ *"thang quy đổi mô phỏng (rule-based, chưa gọi dịch vụ AI bên ngoài)"*.

Đây là thiết kế có chủ ý: đồ án vẫn demo được trên máy không có mạng / không có key.

---

## 4. Tắt AI để test fallback

Muốn kiểm tra hành vi khi không có AI:

```powershell
$env:SMARTIELTS_AI_KEY = ""     # hoặc xoá biến
```
hoặc sửa `appsettings.json`:
```json
"Provider": "RuleBased"
```

---

## 5. ⚠️ Nếu lỡ commit key rồi làm sao?
1. **Thu hồi key ngay** trên trang quản lý (tạo key mới, xoá key cũ)
2. Xoá khỏi file + commit
3. *Xoá khỏi lịch sử git* (nếu repo public):
   ```bash
   git filter-repo --path src/SmartIELTS.WebApi/appsettings.json --invert-paths
   ```
   hoặc dùng BFG Repo-Cleaner
4. Thông báo cả nhóm dùng key mới

**Lưu ý:** chỉ xoá file ở commit mới là **không đủ** — key vẫn nằm trong history.

---

## 6. Chi phí

Model `deepseek-ai/deepseek-v4.1-flash-1017` là reasoning model:

| Thao tác | Token tiêu thụ |
|---|---|
| Chấm 1 bài Writing | ~2.400 reasoning + ~3.500 output |
| Chấm 1 lượt Speaking | ~1.500 reasoning + ~2.000 output |

Một buổi demo 10 bài ≈ 30k–60k token. **Nạp tiền trước khi bảo vệ**, và chạy `check_ai_key.ps1` để xác nhận tài khoản còn tiền (lỗi `402` = hết tiền).

---

## 7. Checklist trước khi bảo vệ

- [ ] `$env:SMARTIELTS_AI_KEY` đã set trên máy demo
- [ ] `.\check_ai_key.ps1` → **THANH CONG**
- [ ] Chấm thử 1 bài Writing thật → feedback trích dẫn cụm từ trong bài (không phải "mô phỏng")
- [ ] Đã nạp tiền tài khoản (tránh 402)
- [ ] Có phương án dự phòng: nếu mất mạng, giải thích được rằng app tự fallback rule-based
- [ ] Chấp nhận chờ 25–35s/bài (đừng bấm nộp nhiều lần)
