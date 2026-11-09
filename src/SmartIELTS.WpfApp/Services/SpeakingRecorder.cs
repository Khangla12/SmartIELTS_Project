using System.IO;
using NAudio.Wave;
namespace SmartIELTS.WpfApp.Services;

/// <summary>
/// Ghi âm câu trả lời Speaking ra file WAV bằng NAudio.
/// <para>
/// File được lưu dưới thư mục cấu hình trong appsettings.json (<c>Recording:Folder</c>),
/// và database chỉ lưu <b>đường dẫn tương đối</b> để dùng chung CSDL giữa nhiều máy trạm.
/// </para>
/// </summary>
public sealed class SpeakingRecorder : IDisposable
{
    private readonly string _folder;
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private string? _currentFileName;

    /// <summary>
    /// Hệ số khuếch đại phần mềm khi ghi (1.0 = giữ nguyên).
    /// Microphone laptop thường thu rất nhỏ, nên mặc định khuếch đại 2.5x.
    /// </summary>
    public float InputGain { get; set; } = 2.5f;

    /// <summary>Mức đỉnh cao nhất đo được trong lần ghi hiện tại (0-32767).</summary>
    public int PeakLevel { get; private set; }

    public SpeakingRecorder(string folder)
    {
        // Luôn dùng đường dẫn tuyệt đối dưới thư mục ứng dụng để không phụ thuộc working directory
        // (chạy bằng dotnet run từ thư mục project hay chạy .exe đều ghi cùng một chỗ).
        _folder = Path.IsPathRooted(folder)
            ? folder
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder);
    }

    public bool IsRecording { get; private set; }

    /// <summary>Đường dẫn tương đối của file đang/đã ghi (ví dụ: recordings/speaking_xxx.wav).</summary>
    public string? LastRelativePath { get; private set; }

    public string? LastFullPath { get; private set; }

    public static bool HasMicrophone() => WaveInEvent.DeviceCount > 0;

    public static IEnumerable<string> ListDevices()
    {
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            yield return $"{i}: {WaveInEvent.GetCapabilities(i).ProductName}";
        }
    }

    /// <summary>Bắt đầu ghi âm. Trả về đường dẫn tương đối của file.</summary>
    public string Start()
    {
        if (IsRecording) throw new InvalidOperationException("Đang trong quá trình ghi âm.");

        if (!HasMicrophone())
            throw new InvalidOperationException("Không tìm thấy thiết bị thu âm (microphone) trên máy này.");

        Directory.CreateDirectory(_folder);

        string fileName = $"speaking_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
        string fullPath = Path.Combine(_folder, fileName);

        _currentFileName = fileName;
        LastFullPath = fullPath;
        LastRelativePath = Path.Combine(_folder, fileName).Replace('\\', '/');
        PeakLevel = 0;

        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 1) // 16kHz mono: đủ cho giọng nói, file nhỏ
        };
        _writer = new WaveFileWriter(fullPath, _waveIn.WaveFormat);

        _waveIn.DataAvailable += (_, args) =>
        {
            if (_writer == null) return;

            // Khuếch đại phần mềm + theo dõi mức đỉnh để hiển thị level meter
            var amplified = Amplify(args.Buffer, args.BytesRecorded, InputGain, out int peak);
            if (peak > PeakLevel) PeakLevel = peak;

            _writer.Write(amplified, 0, args.BytesRecorded);
        };
        _waveIn.RecordingStopped += (_, _) => Stop();

        _waveIn.StartRecording();
        IsRecording = true;

        return LastRelativePath;
    }

    /// <summary>Dừng ghi âm và đóng file. Trả về đường dẫn tương đối (null nếu chưa từng ghi).</summary>
    public string? Stop()
    {
        if (_waveIn != null)
        {
            try
            {
                if (IsRecording) _waveIn.StopRecording();
            }
            catch
            {
                // Thiết bị có thể đã bị ngắt.
            }

            _waveIn.Dispose();
            _waveIn = null;
        }

        _writer?.Dispose();
        _writer = null;
        IsRecording = false;

        // Chuẩn hóa âm lượng file vừa ghi để nghe rõ và nhận dạng chính xác hơn.
        if (LastFullPath != null && File.Exists(LastFullPath))
        {
            try
            {
                NormalizationResult = NormalizeWav(LastFullPath, targetPeak: 0.85f);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SmartIELTS] Không chuẩn hóa được file ghi âm: {ex.Message}");
            }
        }

        return LastRelativePath;
    }

    /// <summary>Kết quả chuẩn hóa lần ghi gần nhất (null nếu chưa chuẩn hóa).</summary>
    public (float AppliedGain, int PeakBefore, int PeakAfter)? NormalizationResult { get; private set; }

    /// <summary>
    /// Chuẩn hóa âm lượng file WAV: đo đỉnh, rồi khuếch đại để đỉnh đạt <paramref name="targetPeak"/>
    /// (mặc định 0.85 = 85% full scale). Không bao giờ giảm âm (chỉ tăng).
    /// <para>
    /// LUÔN ghi lại thành PCM 16-bit 16kHz mono — KHÔNG giữ float 32-bit,
    /// vì Windows Speech Recognition không đọc được WAV float và sẽ treo khi nhận dạng.
    /// </para>
    /// </summary>
    private static (float AppliedGain, int PeakBefore, int PeakAfter) NormalizeWav(string path, float targetPeak)
    {
        var samples = new List<float>();

        using (var reader = new AudioFileReader(path))
        {
            var buffer = new float[8000];
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < read; i++) samples.Add(buffer[i]);
            }
        }

        if (samples.Count == 0) return (1f, 0, 0);

        float peak = 0f;
        foreach (var s in samples)
        {
            float abs = Math.Abs(s);
            if (abs > peak) peak = abs;
        }

        int peakBefore = (int)(peak * short.MaxValue);
        if (peak < 0.0001f) return (1f, peakBefore, peakBefore);

        float gain = targetPeak / peak;
        if (gain < 1f) gain = 1f;       // chỉ tăng, không giảm
        if (gain > 20f) gain = 20f;      // chặn khuếch đại nhiễu nền

        for (int i = 0; i < samples.Count; i++)
        {
            samples[i] = Math.Clamp(samples[i] * gain, -1f, 1f);
        }

        // Ghi lại đúng định dạng SAPI đọc được: PCM 16-bit, 16kHz, mono.
        var targetFormat = new WaveFormat(16000, 16, 1);
        string tempPath = path + ".norm.tmp";

        using (var writer = new WaveFileWriter(tempPath, targetFormat))
        {
            writer.WriteSamples(samples.ToArray(), 0, samples.Count);
        }

        try
        {
            File.Delete(path);
            File.Move(tempPath, path);
        }
        catch
        {
            try { File.Delete(tempPath); } catch { }
            return (1f, peakBefore, peakBefore);
        }

        int peakAfter = (int)(Math.Min(1f, peak * gain) * short.MaxValue);
        return (gain, peakBefore, peakAfter);
    }

    /// <summary>
    /// Khuếch đại mẫu 16-bit PCM, có chống vỡ tiếng (soft clipping).
    /// Trả về buffer mới (không sửa buffer gốc của NAudio).
    /// </summary>
    private static byte[] Amplify(byte[] buffer, int bytesRecorded, float gain, out int peak)
    {
        var output = new byte[bytesRecorded];
        peak = 0;

        if (gain <= 1.0f)
        {
            Array.Copy(buffer, output, bytesRecorded);
            for (int i = 0; i + 1 < bytesRecorded; i += 2)
            {
                int v = Math.Abs((int)BitConverter.ToInt16(buffer, i));
                if (v > peak) peak = v;
            }
            return output;
        }

        for (int i = 0; i + 1 < bytesRecorded; i += 2)
        {
            short sample = BitConverter.ToInt16(buffer, i);
            int amplified = (int)(sample * gain);

            // Soft clip: giới hạn trong khoảng hợp lệ để tránh méo tiếng
            if (amplified > short.MaxValue) amplified = short.MaxValue;
            if (amplified < short.MinValue) amplified = short.MinValue;

            short result = (short)amplified;
            output[i] = (byte)(result & 0xFF);
            output[i + 1] = (byte)((result >> 8) & 0xFF);

            int abs = Math.Abs((int)result);
            if (abs > peak) peak = abs;
        }

        return output;
    }

    /// <summary>Chuyển đường dẫn tương đối lưu trong DB thành đường dẫn tuyệt đối để phát lại.</summary>
    public string? ResolveFullPath(string? relativeOrAbsolutePath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath)) return null;
        if (Path.IsPathRooted(relativeOrAbsolutePath)) return relativeOrAbsolutePath;

        return Path.Combine(_folder, Path.GetFileName(relativeOrAbsolutePath));
    }

    /// <summary>Kiểm tra file ghi âm có tồn tại và có dữ liệu thật không.</summary>
    public (bool Exists, long SizeBytes) InspectRecording(string? relativePath)
    {
        string? full = ResolveFullPath(relativePath);
        if (full == null || !File.Exists(full)) return (false, 0);

        return (true, new FileInfo(full).Length);
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
