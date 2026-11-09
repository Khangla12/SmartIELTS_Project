using System.Globalization;
using System.IO;
using System.Speech.Recognition;

namespace SmartIELTS.WpfApp.Services;

/// <summary>
/// Nhận dạng giọng nói tiếng Anh **offline** bằng Windows Speech Recognition (System.Speech).
/// <para>
/// Có 2 chế độ:
/// </para>
/// <list type="bullet">
///   <item><b>TranscribeFileAsync</b> (khuyến nghị): nhận dạng từ file WAV đã ghi bằng NAudio.
///   Ưu điểm: không tranh chấp microphone với bộ ghi âm, và cho kết quả đầy đủ sau khi nói xong.</item>
///   <item><b>Start / Stop</b>: nghe trực tiếp từ microphone (dùng khi không ghi âm).</item>
/// </list>
/// </summary>
public sealed class SpeechRecognitionService : IDisposable
{
    private SpeechRecognitionEngine? _engine;
    private bool _isListening;

    public bool IsListening => _isListening;

    public static bool HasEnglishRecognizer => GetEnglishRecognizer() != null;

    public static IEnumerable<string> DescribeInstalledRecognizers()
        => SpeechRecognitionEngine.InstalledRecognizers()
            .Select(r => $"{r.Name} ({r.Culture})");

    private static RecognizerInfo? GetEnglishRecognizer()
        => SpeechRecognitionEngine.InstalledRecognizers()
            .FirstOrDefault(r => r.Culture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Nhận dạng toàn bộ nội dung một file WAV (16kHz mono 16-bit do NAudio ghi ra).
    /// Chạy trên background thread để không treo UI.
    /// </summary>
    public Task<string> TranscribeFileAsync(string wavPath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var recognizerInfo = GetEnglishRecognizer();
            if (recognizerInfo == null) return string.Empty;
            if (!File.Exists(wavPath)) return string.Empty;

            using var engine = new SpeechRecognitionEngine(recognizerInfo);
            engine.LoadGrammar(new DictationGrammar());
            engine.LoadGrammar(BuildIeltsPhraseGrammar());

            // Giảm ngưỡng để bắt được cả câu nói nhỏ.
            engine.InitialSilenceTimeout = TimeSpan.FromSeconds(2);
            engine.BabbleTimeout = TimeSpan.FromSeconds(2);
            engine.EndSilenceTimeout = TimeSpan.FromMilliseconds(400);

            try
            {
                engine.SetInputToWaveFile(wavPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SmartIELTS] Không mở được file WAV để nhận dạng: {ex.Message}");
                return string.Empty;
            }

            var parts = new List<string>();

            while (!ct.IsCancellationRequested)
            {
                RecognitionResult? result;
                try
                {
                    result = engine.Recognize();
                }
                catch (InvalidOperationException)
                {
                    break; // hết file
                }

                if (result == null) break; // hết file hoặc hết thời gian chờ

                if (!string.IsNullOrWhiteSpace(result.Text))
                {
                    parts.Add(result.Text.Trim());
                }
            }

            return string.Join(" ", parts).Trim();
        }, ct);
    }

    /// <summary>Nghe trực tiếp từ microphone (dùng khi không bật ghi âm NAudio).</summary>
    public void Start(Action<string> onRecognized, Action<string>? onStatus = null)
    {
        if (_isListening) return;

        var recognizerInfo = GetEnglishRecognizer();
        if (recognizerInfo == null)
        {
            onStatus?.Invoke("Máy chưa cài gói nhận dạng tiếng Anh (Windows Speech Recognition - English). Vui lòng gõ câu trả lời.");
            return;
        }

        _engine = new SpeechRecognitionEngine(recognizerInfo);
        _engine.LoadGrammar(new DictationGrammar());
        _engine.LoadGrammar(BuildIeltsPhraseGrammar());
        _engine.SetInputToDefaultAudioDevice();

        _engine.SpeechRecognized += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Result.Text)) return;
            if (e.Result.Confidence < 0.3f) return;
            onRecognized(e.Result.Text);
        };
        _engine.RecognizeCompleted += (_, e) =>
        {
            onStatus?.Invoke(e.Error == null ? "Đã dừng nhận dạng giọng nói." : $"Lỗi nhận dạng: {e.Error.Message}");
        };

        _engine.RecognizeAsync(RecognizeMode.Multiple);
        _isListening = true;
        onStatus?.Invoke("Đang nghe... hãy nói câu trả lời bằng tiếng Anh.");
    }

    public void Stop()
    {
        if (_engine == null) return;

        try
        {
            _engine.RecognizeAsyncStop();
        }
        catch
        {
            // Thiết bị có thể đã bị ngắt.
        }

        _isListening = false;
    }

    private static Grammar BuildIeltsPhraseGrammar()
    {
        var phrases = new[]
        {
            "in my opinion", "for example", "for instance", "on the other hand",
            "from my perspective", "in terms of", "when it comes to", "as a result",
            "that being said", "to be honest", "furthermore", "nevertheless",
            "artificial intelligence", "sustainable development", "higher education",
            "work-life balance", "social media", "climate change"
        };

        var builder = new GrammarBuilder();
        builder.Append(new Choices(phrases));
        return new Grammar(builder) { Name = "IeltsPhrases" };
    }

    public void Dispose()
    {
        Stop();

        if (_engine != null)
        {
            _engine.Dispose();
            _engine = null;
        }

        GC.SuppressFinalize(this);
    }
}
