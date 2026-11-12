# KIỂM TRA NHANH AI KEY (Deploy AI / DeepSeek-compatible)
# Cách dùng (PowerShell):
#   $env:SMARTIELTS_AI_KEY = "your-key-here"
#   .\check_ai_key.ps1
#
# Script gọi thẳng API 1 lần để xác nhận key + endpoint hoạt động
# TRƯỚC khi demo, tránh phát hiện lỗi ngay lúc bảo vệ.

$ErrorActionPreference = "Stop"

# Đọc cấu hình từ appsettings để không hard-code endpoint
$settingsPath = Join-Path $PSScriptRoot "src\SmartIELTS.WebApi\appsettings.json"
if (-not (Test-Path $settingsPath)) {
    Write-Host "Khong tim thay $settingsPath" -ForegroundColor Red
    exit 1
}

$settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
$ai = $settings.Ai.Grading

$key = [Environment]::GetEnvironmentVariable($ai.ApiKeyEnvironmentVariable)
if (-not $key) { $key = $ai.ApiKey }
if (-not $key) {
    Write-Host "CHUA CO KEY." -ForegroundColor Red
    Write-Host "Chay truoc:" -ForegroundColor Yellow
    Write-Host ('  $env:' + $ai.ApiKeyEnvironmentVariable + ' = "..."') -ForegroundColor Yellow
    exit 1
}

Write-Host "Endpoint : $($ai.BaseUrl)"
Write-Host "Model    : $($ai.Model)"
Write-Host "Key      : $($key.Substring(0, [Math]::Min(8, $key.Length)))..."
Write-Host ""
Write-Host "Dang kiem tra..." -ForegroundColor Cyan

$body = @{
    model    = $ai.Model
    messages = @(
        @{ role = "system"; content = "You are an IELTS examiner. Reply with valid JSON only." }
        @{ role = "user";   content = 'Return exactly: {"overallBand": 7.0, "note": "ok"}' }
    )
    temperature     = 0.2
    max_tokens      = 100
    response_format = @{ type = "json_object" }
    stream          = $false
} | ConvertTo-Json -Depth 10

$headers = @{
    Authorization  = "Bearer $key"
    "Content-Type" = "application/json"
}

$sw = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $resp = Invoke-RestMethod -Uri "$($ai.BaseUrl.TrimEnd('/'))/chat/completions" `
        -Method Post -Headers $headers -Body $body -TimeoutSec 60
    $sw.Stop()

    $content = $resp.choices[0].message.content
    Write-Host ""
    Write-Host "THANH CONG" -ForegroundColor Green
    Write-Host "  Thoi gian   : $([math]::Round($sw.Elapsed.TotalSeconds,2))s"
    Write-Host "  Model       : $($resp.model)"
    Write-Host "  Tokens      : in=$($resp.usage.prompt_tokens) out=$($resp.usage.completion_tokens)"

    if ($resp.usage.reasoning_tokens) {
        Write-Host "  Reasoning   : $($resp.usage.reasoning_tokens) tokens" -ForegroundColor Yellow
        Write-Host "  (Model nay la reasoning model - bai cham day du can 20-35s)" -ForegroundColor DarkGray
    }

    if ([string]::IsNullOrWhiteSpace($content)) {
        Write-Host "  CANH BAO: content rong (model tieu het token vao reasoning)" -ForegroundColor Yellow
        Write-Host "  -> Se tu dong retry voi max_tokens lon hon khi chay app." -ForegroundColor DarkGray
    }
    else {
        Write-Host "  Tra ve      : $content"
    }
}
catch {
    $sw.Stop()
    Write-Host ""
    Write-Host "THAT BAI" -ForegroundColor Red
    Write-Host "  Loi: $($_.Exception.Message)" -ForegroundColor Red

    $status = $_.Exception.Response.StatusCode.value__
    switch ($status) {
        400 { Write-Host "  -> JSON gui di sai dinh dang, hoac model khong ton tai." -ForegroundColor Yellow }
        401 { Write-Host "  -> Key sai hoac het han. Kiem tra lai." -ForegroundColor Yellow }
        402 { Write-Host "  -> Het so du tai khoan." -ForegroundColor Yellow }
        403 { Write-Host "  -> Key khong co quyen dung model nay." -ForegroundColor Yellow }
        404 { Write-Host "  -> Sai BaseUrl. Kiem tra Ai:Grading:BaseUrl trong appsettings.json" -ForegroundColor Yellow }
        429 { Write-Host "  -> Bi gioi han tan suat (rate limit)." -ForegroundColor Yellow }
        500 { Write-Host "  -> Loi phia server AI. Thu lai sau." -ForegroundColor Yellow }
        default {
            if (-not $status) { Write-Host "  -> Khong ket noi duoc. Kiem tra mang / proxy / firewall." -ForegroundColor Yellow }
        }
    }
    exit 1
}
