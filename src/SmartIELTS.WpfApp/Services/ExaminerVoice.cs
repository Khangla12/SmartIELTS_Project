using System.Speech.Synthesis;

namespace SmartIELTS.WpfApp.Services;

/// <summary>
/// Giọng đọc của giám khảo ảo (Text-to-Speech) bằng Windows SAPI.
/// <para>
/// - Offline, không cần API key hay internet (phù hợp phòng thi).
/// - Đọc câu hỏi Speaking bằng giọng Anh (ưu tiên en-US nữ - Zira, giống giọng giám khảo IELTS).
/// - Có thể bật/tắt (thí sinh có thể đọc text thay vì nghe).
/// </para>
/// </summary>
public sealed class ExaminerVoice : IDisposable
{
    private readonly SpeechSynthesizer? _synth;
    private bool _isAvailable;

    public ExaminerVoice()
    {
        try
        {
            _synth = new SpeechSynthesizer();

            // Ưu tiên giọng nữ en-US (Zira) vì giống giọng giám khảo IELTS hơn.
            _synth.SelectVoiceByHints(VoiceGender.Female, VoiceAge.Adult, 0, new System.Globalization.CultureInfo("en-US"));

            _synth.Rate = -1;    // chậm vừa phải, dễ nghe
            _synth.Volume = 100;
            _isAvailable = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SmartIELTS] Không khởi tạo được giọng đọc: {ex.Message}");
            _synth = null;
            _isAvailable = false;
        }
    }

    public bool IsAvailable => _isAvailable;

    /// <summary>Bật/tắt giọng đọc (mặc định bật).</summary>
    public bool IsEnabled { get; set; } = true;

    public bool IsSpeaking => _synth?.State == SynthesizerState.Speaking;

    /// <summary>Đọc to câu hỏi giám khảo. Không chặn UI (SpeakAsync).</summary>
    public void SpeakQuestion(string text)
    {
        if (!_isAvailable || !IsEnabled || string.IsNullOrWhiteSpace(text)) return;

        try
        {
            _synth!.SpeakAsyncCancelAll();
            _synth.SpeakAsync(text);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SmartIELTS] Lỗi đọc câu hỏi: {ex.Message}");
        }
    }

    /// <summary>Đọc chậm hơn (dùng cho nút "Nghe lại câu hỏi").</summary>
    public void RepeatQuestion(string text)
    {
        if (!_synth!.GetType().IsInstanceOfType(_synth)) return;

        try
        {
            int previousRate = _synth.Rate;
            _synth.Rate = -2; // chậm hơn khi nghe lại
            _synth.SpeakAsyncCancelAll();
            _synth.SpeakAsync(text);
            _synth.SpeakCompleted += RestoreRate;

            void RestoreRate(object? s, SpeakCompletedEventArgs e)
            {
                _synth.Rate = previousRate;
                _synth.SpeakCompleted -= RestoreRate;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SmartIELTS] Lỗi nghe lại: {ex.Message}");
        }
    }

    public void Stop()
    {
        try
        {
            _synth?.SpeakAsyncCancelAll();
        }
        catch
        {
            // Bỏ qua
        }
    }

    public void Dispose()
    {
        try
        {
            _synth?.SpeakAsyncCancelAll();
            _synth?.Dispose();
        }
        catch
        {
            // Bỏ qua
        }

        GC.SuppressFinalize(this);
    }
}
