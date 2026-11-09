using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SmartIELTS.Application.DTOs;
using SmartIELTS.Application.Interfaces;
using SmartIELTS.Domain.Common;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;
using SmartIELTS.WpfApp.Services;
using SmartIELTS.WpfApp.Dialogs;
using Microsoft.Win32;

namespace SmartIELTS.WpfApp;
public partial class MainWindow : Window
{
    private readonly WindowsKeyboardHook _keyboardHook;
    private readonly LocalExamManager _examManager;
    private readonly DispatcherTimer _examTimer;
    private readonly DispatcherTimer _autosaveTimer;
    private readonly MediaPlayer _audioPlayer = new();

    private readonly SpeakingRecorder _recorder;
    private readonly ProctoringRealtimeClient _realtime = new();
    private readonly SpeechRecognitionService _speech = new();
    private readonly ExaminerVoice _voice = new();
    private readonly DispatcherTimer _recordingTimer;
    private readonly System.Diagnostics.Stopwatch _recordingWatch = new();
    private readonly DispatcherTimer _speakingTimer;
    private bool _isTeacherDashboardOpen;

    // Trạng thái buổi vấn đáp
    private readonly List<SpeakingTurnDto> _speakingTurns = new();
    private SpeakingPart _currentSpeakingPart = SpeakingPart.Part1Introduction;
    private string _currentQuestion = string.Empty;
    private string _currentHints = string.Empty;
    private int _remainingSpeakingSeconds;
    private bool _isPreparationPhase;

    /// <summary>Hàm đọc câu trả lời hiện tại của từng câu hỏi (TextBox hoặc RadioButton).</summary>
    private readonly Dictionary<Guid, Func<string>> _answerGetters = new();

    private int _remainingSeconds = 165 * 60;
    private SkillType _currentSkill = SkillType.Listening;
    private PendingReviewItemDto? _selectedReview;
    private ExamSessionItemDto? _selectedSession;
    private ExamAdminDto? _selectedAdminExam;
    private ExamSectionAdminDto? _selectedAdminSection;
    private List<ExamSectionAdminDto> _adminSections = new();

    public MainWindow()
    {
        InitializeComponent();

        _keyboardHook = new WindowsKeyboardHook();
        _keyboardHook.OnHotkeyBlocked += KeyboardHook_OnHotkeyBlocked;

        _examManager = new LocalExamManager();
        _recorder = new SpeakingRecorder(_examManager.RecordingFolder);

        // Observer cục bộ: cảnh báo hiện ngay cả khi Web API không chạy (offline-first).
        _examManager.NotificationBus.ViolationLogged += OnViolationLogged;
        _realtime.ViolationReceived += OnRealtimeViolationReceived;

        _examTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _examTimer.Tick += ExamTimer_Tick;

        _autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _autosaveTimer.Tick += AutosaveTimer_Tick;

        _speakingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _speakingTimer.Tick += SpeakingTimer_Tick;

        _recordingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _recordingTimer.Tick += RecordingTimer_Tick;

        _audioPlayer.MediaOpened += (_, _) =>
        {
            // QUAN TRỌNG: MediaPlayer mặc định Volume = 0.5 -> nghe rất nhỏ.
            _audioPlayer.Volume = 1.0;

            BarAudioProgress.Maximum = _audioPlayer.NaturalDuration.HasTimeSpan
                ? _audioPlayer.NaturalDuration.TimeSpan.TotalSeconds
                : 100;
            TxtAudioStatus.Text = $"Audio Listening ({_audioPlayer.NaturalDuration.TimeSpan:mm\\:ss}) - Khóa tua";
        };
        _audioPlayer.MediaEnded += (_, _) =>
        {
            BarAudioProgress.Value = BarAudioProgress.Maximum;
            BtnPlayAudio.Content = "▶ Phát lại (Demo)";
            BtnPlayAudio.IsEnabled = true;
            TxtAudioStatus.Text = "Audio đã phát xong.";
        };
        _audioPlayer.MediaFailed += (_, args) =>
        {
            BtnPlayAudio.IsEnabled = true;
            // URL audio mẫu trong seed không tồn tại -> hiển thị rõ để không gây hiểu nhầm.
            TxtAudioStatus.Text = $"Không tải được audio ({args.ErrorException?.Message ?? "lỗi không xác định"}). Dùng file cục bộ hoặc bỏ qua khi demo.";
        };
    }

    /// <summary>Phát audio Listening thật. Người dùng không có thanh seek nên không thể tua.</summary>
    private void BtnPlayAudio_Click(object sender, RoutedEventArgs e)
    {
        var section = _examManager.CurrentExam?.Sections.FirstOrDefault(s => s.SkillType == SkillType.Listening);
        string mediaUrl = section?.MediaUrl ?? string.Empty;

        // Dữ liệu seed cũ trỏ tới domain không tồn tại -> tự chuyển sang file audio đi kèm ứng dụng.
        if (string.IsNullOrWhiteSpace(mediaUrl) || mediaUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            mediaUrl = LocalListeningAudioPath;
        }

        string? resolved = ResolveMediaPath(mediaUrl);
        if (resolved == null)
        {
            TxtAudioStatus.Text = $"Không tìm thấy file audio Listening ({mediaUrl}).";
            return;
        }

        try
        {
            BtnPlayAudio.IsEnabled = false;
            BtnPlayAudio.Content = "⏸ Đang phát...";
            TxtAudioStatus.Text = "Đang tải audio...";
            _audioPlayer.Open(new Uri(resolved));
            _audioPlayer.Play();
        }
        catch (Exception ex)
        {
            BtnPlayAudio.IsEnabled = true;
            BtnPlayAudio.Content = "▶ Phát Audio";
            TxtAudioStatus.Text = $"Lỗi phát audio: {ex.Message}";
        }
    }

    private const string LocalListeningAudioPath = "media/listening_section1_hotel_reservation.wav";

    /// <summary>Chấp nhận URL tuyệt đối, đường dẫn tuyệt đối, hoặc đường dẫn tương đối trong thư mục ứng dụng.</summary>
    private static string? ResolveMediaPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        // Giữ nguyên URL http/https (nếu sau này cấu hình audio online).
        if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return path;

        if (Path.IsPathRooted(path)) return File.Exists(path) ? path : null;

        string fromBaseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        if (File.Exists(fromBaseDirectory)) return fromBaseDirectory;

        // Chạy bằng `dotnet run` từ thư mục project (trước khi build copy file).
        string fromWorkingDirectory = Path.Combine(Environment.CurrentDirectory, path);
        return File.Exists(fromWorkingDirectory) ? fromWorkingDirectory : null;
    }

    private void BtnQuickRole_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string role)
        {
            if (role == "student")
            {
                TxtUsername.Text = "student";
                TxtPassword.Password = "Student@123";
            }
            else if (role == "teacher")
            {
                TxtUsername.Text = "teacher";
                TxtPassword.Password = "Teacher@123";
            }
            else if (role == "admin")
            {
                TxtUsername.Text = "admin";
                TxtPassword.Password = "Admin@123";
            }
        }
    }

    private void KeyboardHook_OnHotkeyBlocked(string hotkey)
    {
        Dispatcher.Invoke(() =>
        {
            TxtProctoringAlert.Foreground = (SolidColorBrush)FindResource("DangerRed");
            TxtProctoringAlert.Text = $"⚠️ CẢNH BÁO VI PHẠM KIOSK: Hệ thống đã chặn phím nóng [{hotkey}]!";
            _ = _examManager.LogProctoringViolationAsync(ViolationType.HotkeyBlocked, $"Blocked hotkey: {hotkey}");        });
    }

    /// <summary>Cảnh báo đến từ chính máy trạm này (đi qua Observer bus, không cần mạng).</summary>
    private void OnViolationLogged(ProctoringNotification notification)
    {
        Dispatcher.Invoke(() => ShowViolation(notification));
    }

    /// <summary>Cảnh báo đến từ máy trạm khác qua SignalR (khi Web API đang chạy).</summary>
    private void OnRealtimeViolationReceived(ProctoringNotification notification)
    {
        Dispatcher.Invoke(() => ShowViolation(notification));
    }

    private void ShowViolation(ProctoringNotification notification)
    {
        string label = $"[{notification.Timestamp:HH:mm:ss}] {notification.CandidateName} - {notification.ViolationType}: {notification.Description}";

        TxtProctoringAlert.Foreground = (SolidColorBrush)FindResource("DangerRed");
        TxtProctoringAlert.Text = $"⚠️ {label}";

        if (_isTeacherDashboardOpen)
        {
            // Chèn thẳng vào danh sách đang hiển thị để giám thị thấy ngay, không cần bấm Làm mới.
            var rows = ListViolations.ItemsSource as IEnumerable<ProctoringLogItemDto>;
            var updated = new List<ProctoringLogItemDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    SessionId = notification.SessionId,
                    CandidateName = notification.CandidateName,
                    Username = "-",
                    Timestamp = notification.Timestamp,
                    ViolationType = notification.ViolationType.ToString(),
                    Description = notification.Description
                }
            };
            if (rows != null) updated.AddRange(rows);

            ListViolations.ItemsSource = updated;
            TxtKpiViolations.Text = $"{updated.Count} Cảnh báo";
        }
    }

    private async void BtnStartExam_Click(object sender, RoutedEventArgs e)
    {
        string username = TxtUsername.Text.Trim();
        string password = TxtPassword.Password;

        try
        {
            var user = await _examManager.LoginAsync(username, password);
            if (user == null)
            {
                MessageBox.Show("Tài khoản hoặc mật khẩu không chính xác!", "Lỗi xác thực", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Teacher / Admin vào bàn giám sát & duyệt điểm
            if (user.Role == UserRole.Teacher || user.Role == UserRole.Admin)
            {
                OpenTeacherDashboard(user);
                return;
            }

            var exam = await _examManager.GetActiveExamAsync();
            if (exam == null)
            {
                MessageBox.Show("Không tìm thấy đề thi hoạt động!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await _examManager.StartSessionAsync();

            // Thời gian thi lấy theo cấu hình đề thi thay vì hard-code.
            _remainingSeconds = Math.Max(1, exam.TotalDurationMinutes) * 60;
            UpdateTimerText();

            TxtCandidateBadge.Text = $"Thí sinh: {user.FullName} ({user.Username})";
            ViewLogin.Visibility = Visibility.Collapsed;
            ViewTeacherDashboard.Visibility = Visibility.Collapsed;
            ViewResult.Visibility = Visibility.Collapsed;
            ViewExam.Visibility = Visibility.Visible;
            BtnLogout.Visibility = Visibility.Visible;
            BadgeKiosk.Visibility = Visibility.Visible;
            BtnToggleKiosk.Visibility = Visibility.Visible;

            try
            {
                _keyboardHook.Install();
            }
            catch { /* Không đủ quyền thì bỏ qua, không chặn luồng thi */ }

            _examTimer.Start();
            _autosaveTimer.Start();

            LoadSkillView(SkillType.Listening);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi kết nối cơ sở dữ liệu hoặc hệ thống:\n{ex.Message}\n\nNếu đang dùng SQL Server, hãy đảm bảo SQL Server đang chạy hoặc cấu hình DatabaseProvider = 'Sqlite' trong appsettings.json.",
                "Lỗi Khởi Động Bài Thi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void LoadSkillView(SkillType skill)
    {
        _currentSkill = skill;
        _answerGetters.Clear();

        var activeBrush = (SolidColorBrush)FindResource("PrimaryTeal");
        var inactiveBrush = Brushes.Transparent;
        var activeText = Brushes.White;
        var inactiveText = (SolidColorBrush)FindResource("BodyText");

        TabListening.Background = skill == SkillType.Listening ? activeBrush : inactiveBrush;
        TabReading.Background = skill == SkillType.Reading ? activeBrush : inactiveBrush;
        TabWriting.Background = skill == SkillType.Writing ? activeBrush : inactiveBrush;
        TabSpeaking.Background = skill == SkillType.Speaking ? activeBrush : inactiveBrush;

        TabListening.Foreground = skill == SkillType.Listening ? activeText : inactiveText;
        TabReading.Foreground = skill == SkillType.Reading ? activeText : inactiveText;
        TabWriting.Foreground = skill == SkillType.Writing ? activeText : inactiveText;
        TabSpeaking.Foreground = skill == SkillType.Speaking ? activeText : inactiveText;

        PanelListening.Visibility = skill == SkillType.Listening ? Visibility.Visible : Visibility.Collapsed;
        PanelReading.Visibility = skill == SkillType.Reading ? Visibility.Visible : Visibility.Collapsed;
        PanelWriting.Visibility = skill == SkillType.Writing ? Visibility.Visible : Visibility.Collapsed;
        PanelSpeaking.Visibility = skill == SkillType.Speaking ? Visibility.Visible : Visibility.Collapsed;

        var section = _examManager.CurrentExam?.Sections.FirstOrDefault(s => s.SkillType == skill);
        if (section == null) return;

        switch (skill)
        {
            case SkillType.Listening:
                StackListeningQuestions.Children.Clear();
                foreach (var q in section.Questions.OrderBy(x => x.QuestionNumber))
                {
                    StackListeningQuestions.Children.Add(CreateQuestionControl(q));
                }
                break;

            case SkillType.Reading:
                TxtReadingPassage.Text = section.PassageText ?? string.Empty;
                StackReadingQuestions.Children.Clear();
                foreach (var q in section.Questions.OrderBy(x => x.QuestionNumber))
                {
                    StackReadingQuestions.Children.Add(CreateQuestionControl(q));
                }
                break;

            case SkillType.Writing:
                TxtWritingPrompt.Text = section.PassageText ?? string.Empty;
                UpdateWordCount();
                break;

            case SkillType.Speaking:
                _recorder.Stop();
                UpdateRecordingUi(stopped: true);
                _ = InitializeSpeakingAsync();
                break;
        }

        TxtActionStatus.Text = $"Đang làm phần thi: {skill}";

        // Khôi phục bản nháp đã lưu (nếu có) - Offline-First.
        await RestoreDraftAsync(skill, section);
    }

    /// <summary>
    /// Tạo control trả lời đúng theo loại câu hỏi: trắc nghiệm -> RadioButton, còn lại -> TextBox.
    /// </summary>
    private UIElement CreateQuestionControl(QuestionDto q)
    {
        var border = new Border
        {
            Background = (SolidColorBrush)FindResource("BgSurface"),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 14),
            Margin = new Thickness(0, 0, 0, 10),
            BorderBrush = (SolidColorBrush)FindResource("BorderSubtle"),
            BorderThickness = new Thickness(1)
        };

        var stack = new StackPanel();

        // Tiêu đề: badge số câu + nội dung câu hỏi
        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

        header.Children.Add(new Border
        {
            Background = (SolidColorBrush)FindResource("PrimarySoft"),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = $"Câu {q.QuestionNumber}",
                Foreground = (SolidColorBrush)FindResource("PrimaryTeal"),
                FontWeight = FontWeights.Bold,
                FontSize = 12
            }
        });

        header.Children.Add(new TextBlock
        {
            Text = q.QuestionText,
            Foreground = (SolidColorBrush)FindResource("TextPrimary"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 640,
            VerticalAlignment = VerticalAlignment.Center
        });

        stack.Children.Add(header);

        var options = ParseOptions(q.OptionsJson);

        if (options.Count > 0)
        {
            var groupName = $"Q_{q.Id:N}";
            var buttons = new List<RadioButton>();

            foreach (var option in options)
            {
                var radio = new RadioButton
                {
                    Content = option,
                    GroupName = groupName,
                    Foreground = (SolidColorBrush)FindResource("BodyText"),
                    FontSize = 13,
                    Margin = new Thickness(0, 4, 0, 4),
                    Tag = option
                };
                buttons.Add(radio);
                stack.Children.Add(radio);
            }

            _answerGetters[q.Id] = () =>
                buttons.FirstOrDefault(b => b.IsChecked == true)?.Tag as string ?? string.Empty;
        }
        else
        {
            var txtAnswer = new TextBox
            {
                Height = 38,
                FontSize = 13,
                Margin = new Thickness(0, 4, 0, 0)
            };
            stack.Children.Add(txtAnswer);

            _answerGetters[q.Id] = () => txtAnswer.Text;
        }

        border.Child = stack;
        return border;
    }

    private static List<string> ParseOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson)) return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(optionsJson) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    private void TabSkill_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            if (Enum.TryParse<SkillType>(tag, out var skill))
            {
                LoadSkillView(skill);
            }
        }
    }

    private async void BtnSubmitCurrentSkill_Click(object sender, RoutedEventArgs e)
    {
        if (_examManager.CurrentSession == null)
        {
            MessageBox.Show("Chưa có ca thi đang hoạt động. Vui lòng đăng nhập lại.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var answers = _answerGetters.ToDictionary(k => k.Key, v => v.Value());
        string? essay = _currentSkill == SkillType.Writing ? TxtEssayEditor.Text : null;
        string? audio = _currentSkill == SkillType.Speaking ? "speaking_candidate.wav" : null;

        TxtActionStatus.Text = $"Đang chấm điểm kỹ năng {_currentSkill} qua bộ máy Grader Strategy...";

        try
        {
            var result = await _examManager.SubmitSkillAsync(_currentSkill, answers, essay, audio);

            string rawLine = result.TotalQuestions > 0
                ? $"\nSố câu đúng: {result.RawScore:0}/{result.TotalQuestions}"
                : string.Empty;

            MessageBox.Show($"Chấm điểm hoàn tất!\nKỹ năng: {_currentSkill}{rawLine}\nĐiểm Band: {result.BandScore:0.0}\nNhận xét: {result.FeedbackComments}",
                "Kết quả chấm điểm", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể chấm điểm kỹ năng {_currentSkill}:\n{ex.Message}", "Lỗi chấm điểm", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (_currentSkill == SkillType.Speaking)
        {
            await ShowFinalReportAsync();
            return;
        }

        var nextSkill = _currentSkill switch
        {
            SkillType.Listening => SkillType.Reading,
            SkillType.Reading => SkillType.Writing,
            SkillType.Writing => SkillType.Speaking,
            _ => SkillType.Listening
        };

        LoadSkillView(nextSkill);
    }

    private async void BtnSubmitSpeaking_Click(object sender, RoutedEventArgs e)
    {
        await FinalizeSpeakingAsync();
    }

    private void BtnStartRecording_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string relativePath = _recorder.Start();
            UpdateRecordingUi(stopped: false);

            _recordingWatch.Restart();
            _recordingTimer.Start();

            TxtRecordingStatus.Text = $"🔴 ĐANG GHI ÂM... (0.0s) → {Path.GetFileName(relativePath)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể bắt đầu ghi âm:\n{ex.Message}\n\nKiểm tra microphone trong Cài đặt > Âm thanh.",
                "Lỗi thiết bị thu âm", MessageBoxButton.OK, MessageBoxImage.Warning);
            UpdateRecordingUi(stopped: true);
        }
    }

    private void RecordingTimer_Tick(object? sender, EventArgs e)
    {
        if (!_recorder.IsRecording)
        {
            _recordingTimer.Stop();
            return;
        }

        // Hiển thị mức âm thanh để thí sinh biết mic đang thu được tiếng.
        int peak = _recorder.PeakLevel;
        int levelPercent = (int)Math.Min(100, peak * 100.0 / 32767);
        string bar = new string('█', Math.Max(0, levelPercent / 5)).PadRight(20, '·');

        string quality = levelPercent switch
        {
            < 3 => "quá nhỏ",
            < 15 => "hơi nhỏ",
            < 70 => "tốt",
            _ => "hơi lớn"
        };

        TxtRecordingStatus.Text = $"🔴 ĐANG GHI ÂM {_recordingWatch.Elapsed.TotalSeconds:0.0}s  [{bar}] {levelPercent}% ({quality})";
        TxtRecordingStatus.Foreground = levelPercent < 3
            ? (SolidColorBrush)FindResource("DangerRed")
            : (SolidColorBrush)FindResource("SuccessGreen");
    }

    private void BtnStopRecording_Click(object sender, RoutedEventArgs e)
    {
        _recordingTimer.Stop();
        _recordingWatch.Stop();

        string? path = _recorder.Stop();
        UpdateRecordingUi(stopped: true);

        if (string.IsNullOrWhiteSpace(path)) return;

        string? fullPath = _recorder.ResolveFullPath(path);
        double sizeKb = fullPath != null && File.Exists(fullPath) ? new FileInfo(fullPath).Length / 1024.0 : 0;
        double seconds = _recordingWatch.Elapsed.TotalSeconds;

        var norm = _recorder.NormalizationResult;
        string normInfo = norm.HasValue && norm.Value.AppliedGain > 1.05f
            ? $" | chuẩn hóa âm lượng ×{norm.Value.AppliedGain:0.0} (peak {norm.Value.PeakBefore} → {norm.Value.PeakAfter})"
            : string.Empty;

        TxtRecordingStatus.Text = $"✅ Đã lưu: {Path.GetFileName(path)} ({seconds:0.0}s, {sizeKb:0} KB){normInfo}";
        TxtRecordingStatus.Foreground = (SolidColorBrush)FindResource("SuccessGreen");

        // Tự động nhận dạng giọng nói thành text (sau khi đã dừng ghi âm để không tranh microphone).
        _ = TranscribeRecordingAsync(path, seconds);
    }

    /// <summary>
    /// Sau khi ghi âm xong: nhận dạng giọng nói và đổ text vào ô trả lời,
    /// để thí sinh thấy đúng những gì mình vừa nói và có thể sửa lại.
    /// </summary>
    private async Task TranscribeRecordingAsync(string relativePath, double seconds)
    {
        if (!SpeechRecognitionService.HasEnglishRecognizer)
        {
            TxtSpeechStatus.Text = "Máy chưa cài gói nhận dạng tiếng Anh — không thể tự chuyển lời nói thành text.";
            return;
        }

        if (seconds < 0.5)
        {
            TxtSpeechStatus.Text = "Bản ghi quá ngắn để nhận dạng.";
            return;
        }

        string? fullPath = _recorder.ResolveFullPath(relativePath);
        if (string.IsNullOrWhiteSpace(fullPath)) return;

        TxtSpeechStatus.Foreground = (SolidColorBrush)FindResource("AccentBlue");
        TxtSpeechStatus.Text = "🎧 Đang nhận dạng giọng nói của bạn...";
        BtnPlayRecording.IsEnabled = false;

        try
        {
            string transcript = await _speech.TranscribeFileAsync(fullPath);

            if (string.IsNullOrWhiteSpace(transcript))
            {
                TxtSpeechStatus.Foreground = (SolidColorBrush)FindResource("WarningYellow");
                TxtSpeechStatus.Text = "Không nhận dạng được nội dung (nói nhỏ / ồn / không rõ). Bạn có thể gõ lại hoặc ghi âm lại.";
                return;
            }

            // Hiển thị đúng những gì thí sinh vừa nói
            TxtSpeakingTranscript.Text = transcript;

            TxtSpeechStatus.Foreground = (SolidColorBrush)FindResource("SuccessGreen");
            TxtSpeechStatus.Text = $"✅ Đã chuyển lời nói thành text ({transcript.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length} từ). Kiểm tra và sửa nếu cần, rồi bấm Gửi Câu Trả Lời.";
        }
        catch (Exception ex)
        {
            TxtSpeechStatus.Foreground = (SolidColorBrush)FindResource("DangerRed");
            TxtSpeechStatus.Text = $"Lỗi nhận dạng giọng nói: {ex.Message}";
        }
        finally
        {
            BtnPlayRecording.IsEnabled = !string.IsNullOrWhiteSpace(_recorder.LastRelativePath);
        }
    }

    private void BtnPlayRecording_Click(object sender, RoutedEventArgs e)
    {
        string? fullPath = _recorder.ResolveFullPath(_recorder.LastRelativePath);
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
        {
            MessageBox.Show("Chưa có file ghi âm để phát lại.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            _audioPlayer.Close();
            _audioPlayer.Open(new Uri(fullPath));

            // MediaPlayer mặc định Volume = 0.5 (nghe rất nhỏ) -> đặt tối đa.
            _audioPlayer.Volume = 1.0;
            _audioPlayer.Play();

            double sizeKb = new FileInfo(fullPath).Length / 1024.0;
            TxtRecordingStatus.Text = $"🔊 Đang phát lại: {Path.GetFileName(fullPath)} ({sizeKb:0} KB) — volume 100%";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không phát được file ghi âm:\n{ex.Message}", "Lỗi phát lại", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // =====================================================================
    // SPEAKING: GIÁM KHẢO ẢO - HỘI THOẠI NHIỀU LƯỢT
    // =====================================================================

    private async Task InitializeSpeakingAsync()
    {
        _speakingTurns.Clear();
        _currentSpeakingPart = SpeakingPart.Part1Introduction;
        ListSpeakingTurns.ItemsSource = null;
        CardTurnFeedback.Visibility = Visibility.Collapsed;
        TxtSpeakingTranscript.Text = string.Empty;
        TxtSpeakingStats.Text = "0 lượt • 0 từ";

        var prompts = await _examManager.GetSpeakingPromptsAsync();

        if (prompts.Count > 0)
        {
            // Có kịch bản: dùng câu hỏi seed (kèm text gợi ý đầy đủ).
            var first = prompts[0];
            _currentQuestion = first.Question;
            _currentHints = first.CandidatePromptText;
            _currentSpeakingPart = first.Part;
            _remainingSpeakingSeconds = first.PreparationSeconds > 0
                ? first.PreparationSeconds
                : first.ResponseSeconds;
            _isPreparationPhase = first.PreparationSeconds > 0;
        }
        else
        {
            _currentQuestion = "Let's talk about where you live. Do you live in a house or an apartment?";
            _currentHints = "Hãy trả lời 2-3 câu: loại nhà + 1 lý do bạn thích.";
            _remainingSpeakingSeconds = 40;
            _isPreparationPhase = false;
        }

        RenderSpeakingQuestion();
        StartSpeakingTimer();

        // Giám khảo ảo đọc to câu hỏi đầu tiên.
        _voice.SpeakQuestion(_currentQuestion);

        TxtSpeechStatus.Text = SpeechRecognitionService.HasEnglishRecognizer
            ? "Nhận dạng giọng nói tiếng Anh: sẵn sàng (offline). Bạn có thể nói hoặc gõ câu trả lời."
            : "Máy chưa cài gói nhận dạng tiếng Anh — vui lòng gõ câu trả lời.";
    }

    private void RenderSpeakingQuestion()
    {
        TxtSpeakingPartBadge.Text = _currentSpeakingPart switch
        {
            SpeakingPart.Part1Introduction => "PART 1 — INTRODUCTION & INTERVIEW",
            SpeakingPart.Part2LongTurn => "PART 2 — LONG TURN (CUE CARD)",
            SpeakingPart.Part3Discussion => "PART 3 — TWO-WAY DISCUSSION",
            _ => _currentSpeakingPart.ToString()
        };

        TxtExaminerQuestion.Text = _currentQuestion;
        TxtCandidateGuide.Text = string.IsNullOrWhiteSpace(_currentHints)
            ? "Hãy trả lời bằng tiếng Anh, càng chi tiết càng tốt."
            : _currentHints;

        TxtSpeakingProgress.Text = $"Lượt đã trả lời: {_speakingTurns.Count}";
        BarSpeakingProgress.Value = Math.Min(_speakingTurns.Count, BarSpeakingProgress.Maximum);

        BtnSkipPreparation.Visibility = _isPreparationPhase ? Visibility.Visible : Visibility.Collapsed;
        TxtSpeakingState.Text = _isPreparationPhase
            ? "Đang trong thời gian chuẩn bị (Part 2)."
            : "Bấm Ghi Âm hoặc nhận dạng giọng nói để trả lời.";
    }

    private void StartSpeakingTimer()
    {
        _speakingTimer.Stop();
        _speakingTimer.Start();
        UpdateSpeakingTimerText();
    }

    private void SpeakingTimer_Tick(object? sender, EventArgs e)
    {
        if (_remainingSpeakingSeconds > 0)
        {
            _remainingSpeakingSeconds--;
            UpdateSpeakingTimerText();

            if (_remainingSpeakingSeconds == 0)
            {
                if (_isPreparationPhase)
                {
                    FinishPreparation();
                }
                else
                {
                    TxtSpeakingState.Text = "Đã hết thời gian trả lời — hãy bấm Gửi Câu Trả Lời.";
                }
            }

            return;
        }

        UpdateSpeakingTimerText();
    }

    private void UpdateSpeakingTimerText()
        => TxtSpeakingTimer.Text = $"{_remainingSpeakingSeconds / 60:D2}:{_remainingSpeakingSeconds % 60:D2}";

    private void FinishPreparation()
    {
        _isPreparationPhase = false;
        _remainingSpeakingSeconds = 120;
        BtnSkipPreparation.Visibility = Visibility.Collapsed;
        TxtSpeakingState.Text = "Hết thời gian chuẩn bị. Hãy nói liên tục 1.5–2 phút.";
        UpdateSpeakingTimerText();
    }

    private void BtnSkipPreparation_Click(object sender, RoutedEventArgs e) => FinishPreparation();

    /// <summary>Nghe lại câu hỏi hiện tại (đọc chậm hơn).</summary>
    private void BtnReplayQuestion_Click(object sender, RoutedEventArgs e)
    {
        if (!_voice.IsAvailable)
        {
            MessageBox.Show("Máy chưa cài giọng đọc tiếng Anh (Windows Speech). Hãy đọc câu hỏi trên màn hình.",
                "Không có giọng đọc", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _voice.RepeatQuestion(_currentQuestion);
    }

    /// <summary>Bật/tắt giọng đọc giám khảo (phòng thi có thể yêu cầu tắt âm).</summary>
    private void BtnToggleVoice_Click(object sender, RoutedEventArgs e)
    {
        _voice.IsEnabled = !_voice.IsEnabled;
        if (!_voice.IsEnabled) _voice.Stop();

        BtnToggleVoice.Content = _voice.IsEnabled ? "Giọng: BẬT" : "Giọng: TẮT";
        BtnToggleVoice.Background = _voice.IsEnabled
            ? (SolidColorBrush)FindResource("PrimaryTeal")
            : (SolidColorBrush)FindResource("BgSubtle");
        BtnToggleVoice.Foreground = _voice.IsEnabled
            ? Brushes.White
            : (SolidColorBrush)FindResource("BodyText");
    }

    private async void BtnSubmitTurn_Click(object sender, RoutedEventArgs e)
    {
        if (_examManager.CurrentSession == null)
        {
            MessageBox.Show("Chưa có ca thi đang hoạt động.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string answer = TxtSpeakingTranscript.Text.Trim();
        if (answer.Length == 0)
        {
            MessageBox.Show("Vui lòng nói (hoặc gõ) câu trả lời trước khi gửi.",
                "Thiếu câu trả lời", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_speech.IsListening)
        {
            _speech.Stop();
            BtnStartSpeechToText.Content = "🎧 Nhận Dạng Giọng Nói";
        }

        BtnSubmitTurn.IsEnabled = false;

        try
        {
            string? audioPath = _recorder.LastRelativePath;

            var result = await _examManager.SubmitSpeakingTurnAsync(new SpeakingTurnRequestDto
            {
                SessionId = _examManager.CurrentSession.Id,
                Question = _currentQuestion,
                Part = _currentSpeakingPart,
                Answer = answer,
                AudioRecordUrl = audioPath,
                PreviousTurns = _speakingTurns.ToList()
            });

            // Lưu lượt vào lịch sử
            _speakingTurns.Add(new SpeakingTurnDto
            {
                TurnNumber = _speakingTurns.Count + 1,
                Part = _currentSpeakingPart,
                Question = _currentQuestion,
                Answer = answer
            });

            ShowTurnFeedback(result);
            UpdateSpeakingHistory();

            if (result.IsFinished || string.IsNullOrWhiteSpace(result.NextQuestion))
            {
                TxtSpeakingState.Text = "Buổi vấn đáp đã kết thúc. Bấm Chốt Điểm Speaking.";
                _speakingTimer.Stop();
                BtnSubmitTurn.IsEnabled = false;
                return;
            }

            // Chuyển sang câu hỏi tiếp theo
            _currentSpeakingPart = result.NextPart;
            _currentQuestion = result.NextQuestion!;
            _currentHints = result.NextQuestionHints ?? string.Empty;
            _isPreparationPhase = _currentSpeakingPart == SpeakingPart.Part2LongTurn;
            _remainingSpeakingSeconds = _isPreparationPhase ? 60 : DefaultResponseSeconds(_currentSpeakingPart);

            TxtSpeakingTranscript.Text = string.Empty;
            RenderSpeakingQuestion();
            StartSpeakingTimer();

            // Giám khảo đọc câu hỏi tiếp theo
            _voice.SpeakQuestion(_currentQuestion);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không chấm được câu trả lời:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnSubmitTurn.IsEnabled = true;
        }
    }

    private static int DefaultResponseSeconds(SpeakingPart part) => part switch
    {
        SpeakingPart.Part1Introduction => 40,
        SpeakingPart.Part2LongTurn => 120,
        _ => 90
    };

    private void ShowTurnFeedback(SpeakingTurnResultDto result)
    {
        CardTurnFeedback.Visibility = Visibility.Visible;
        TxtTurnBand.Text = $"Band {result.TurnBand:0.0}";
        TxtTurnFeedback.Text = $"{result.TurnFeedback}\n({result.WordCount} từ — {result.LengthAssessment})";
        TxtTurnCriteria.Text =
            $"Fluency: {result.FluencyCoherence:0.0}  •  Pronunciation: {result.Pronunciation:0.0}  •  " +
            $"Lexical: {result.LexicalResource:0.0}  •  Grammar: {result.GrammaticalRangeAccuracy:0.0}";

        // Hiển thị lỗi/cảnh báo có màu
        var items = new List<TextBlock>();
        foreach (var h in result.Highlights.Take(4))
        {
            var brush = h.SeverityColor switch
            {
                "Red" => (SolidColorBrush)FindResource("DangerRed"),
                "Yellow" => (SolidColorBrush)FindResource("WarningYellow"),
                _ => (SolidColorBrush)FindResource("SuccessGreen")
            };

            items.Add(new TextBlock
            {
                Text = $"• [{h.ErrorType}] \"{h.OriginalText}\" → {h.Suggestion}",
                Foreground = brush,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
        }

        if (items.Count == 0)
        {
            items.Add(new TextBlock
            {
                Text = "• Không phát hiện lỗi ngữ pháp rõ ràng.",
                Foreground = (SolidColorBrush)FindResource("SuccessGreen"),
                FontSize = 11
            });
        }

        ListTurnHighlights.ItemsSource = items;
    }

    private void UpdateSpeakingHistory()
    {
        ListSpeakingTurns.ItemsSource = null;
        ListSpeakingTurns.ItemsSource = _speakingTurns.Select(t => new
        {
            Header = $"Lượt {t.TurnNumber} • {DescribePartShort(t.Part)}",
            Answer = t.Answer
        }).ToList();

        int totalWords = _speakingTurns.Sum(t => t.Answer.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        double avgBand = _speakingTurns.Count > 0 ? _examManager.LastTurnBand : 0;

        TxtSpeakingStats.Text = $"{_speakingTurns.Count} lượt • {totalWords} từ" +
            (avgBand > 0 ? $" • Band gần nhất {avgBand:0.0}" : string.Empty);
    }

    private static string DescribePartShort(SpeakingPart part) => part switch
    {
        SpeakingPart.Part1Introduction => "Part 1",
        SpeakingPart.Part2LongTurn => "Part 2",
        SpeakingPart.Part3Discussion => "Part 3",
        _ => part.ToString()
    };

    private async void BtnFinalizeSpeaking_Click(object sender, RoutedEventArgs e)
    {
        await FinalizeSpeakingAsync();
    }

    private async Task FinalizeSpeakingAsync()
    {
        if (_speakingTurns.Count == 0)
        {
            MessageBox.Show("Bạn chưa trả lời câu hỏi nào. Vui lòng trả lời ít nhất một câu.",
                "Chưa có câu trả lời", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Chốt điểm Speaking với {_speakingTurns.Count} câu trả lời?\nSau khi chốt, bài nộp không thể sửa.",
            "Xác nhận chốt điểm", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        _speakingTimer.Stop();
        if (_speech.IsListening) _speech.Stop();
        if (_recorder.IsRecording) { _recorder.Stop(); UpdateRecordingUi(stopped: true); }

        TxtActionStatus.Text = "Đang chấm điểm toàn bộ buổi vấn đáp...";

        try
        {
            var result = await _examManager.FinalizeSpeakingAsync(_speakingTurns, _recorder.LastRelativePath);

            MessageBox.Show(
                $"✅ Đã chốt điểm Speaking!\n\nBand: {result.BandScore:0.0}\n" +
                $"Fluency: {result.CriterionScore1:0.0} • Pronunciation: {result.CriterionScore2:0.0}\n" +
                $"Lexical: {result.CriterionScore3:0.0} • Grammar: {result.CriterionScore4:0.0}\n" +
                $"Số lượt: {_speakingTurns.Count}\n\nKết quả đang chờ giảng viên duyệt.",
                "Hoàn tất Speaking", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không chốt được điểm Speaking:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        await ShowFinalReportAsync();
    }

    private void BtnStartSpeechToText_Click(object sender, RoutedEventArgs e)
    {
        if (_speech.IsListening)
        {
            _speech.Stop();
            BtnStartSpeechToText.Content = "🎧 Nhận Dạng Giọng Nói";
            TxtSpeechStatus.Text = "Đã dừng nhận dạng.";
            return;
        }

        BtnStartSpeechToText.Content = "⏹ Dừng Nghe";
        TxtSpeechStatus.Text = "Đang khởi động micro...";

        _speech.Start(
            recognized => Dispatcher.Invoke(() =>
            {
                // Nối thêm vào câu trả lời, thí sinh vẫn sửa được.
                TxtSpeakingTranscript.Text = string.IsNullOrWhiteSpace(TxtSpeakingTranscript.Text)
                    ? recognized
                    : TxtSpeakingTranscript.Text.TrimEnd() + " " + recognized;
            }),
            status => Dispatcher.Invoke(() =>
            {
                TxtSpeechStatus.Text = status;
                if (!_speech.IsListening) BtnStartSpeechToText.Content = "🎧 Nhận Dạng Giọng Nói";
            }));
    }

    private void UpdateRecordingUi(bool stopped)
    {
        BtnStartRecording.IsEnabled = stopped;
        BtnStopRecording.IsEnabled = !stopped;
        BtnPlayRecording.IsEnabled = stopped && !string.IsNullOrWhiteSpace(_recorder.LastRelativePath);

        if (!stopped)
        {
            TxtRecordingStatus.Foreground = (SolidColorBrush)FindResource("DangerRed");
            TxtRecordingStatus.Text = "🔴 ĐANG GHI ÂM... hãy nói câu trả lời.";
            return;
        }

        TxtRecordingStatus.Foreground = (SolidColorBrush)FindResource(
            SpeakingRecorder.HasMicrophone() ? "SuccessGreen" : "WarningYellow");

        TxtRecordingStatus.Text = !SpeakingRecorder.HasMicrophone()
            ? "Không tìm thấy microphone trên máy này."
            : string.IsNullOrWhiteSpace(_recorder.LastRelativePath)
                ? "Chưa có bản ghi âm."
                : $"Đã lưu bản ghi âm: {Path.GetFileName(_recorder.LastRelativePath)}";
    }

    private async Task ShowFinalReportAsync()
    {
        _examTimer.Stop();
        _autosaveTimer.Stop();
        _keyboardHook.IsEnabled = false;

        ViewExam.Visibility = Visibility.Collapsed;
        ViewResult.Visibility = Visibility.Visible;

        var summary = await _examManager.GetSessionSummaryAsync();

        // Hiển thị đúng số liệu thực tế, không "làm đẹp" bằng giá trị mặc định.
        TxtOverallBand.Text = summary.OverallBandScore.HasValue
            ? summary.OverallBandScore.Value.ToString("0.0")
            : "--";

        TxtScoreListening.Text = FormatSkillLine("🎧 Listening", summary.ListeningBand, "Dung sai hoa/thường, biến thể Anh - Mỹ");
        TxtScoreReading.Text = FormatSkillLine("📖 Reading", summary.ReadingBand, "Khớp đáp án chính xác");
        TxtScoreWriting.Text = FormatSkillLine("✍️ Writing", summary.WritingBand, "4 tiêu chí Cambridge");
        TxtScoreSpeaking.Text = FormatSkillLine("🎙️ Speaking", summary.SpeakingBand, "4 tiêu chí Cambridge");

        if (summary.Submissions.Count < 4)
        {
            TxtDiagnosticFeedback.Text = "Bạn chưa hoàn thành đủ 4 kỹ năng nên Overall Band chưa được chốt. Hãy làm nốt các phần còn lại.";
        }

        var writingReport = summary.Submissions
            .FirstOrDefault(s => s.SkillType == SkillType.Writing)?.Reports.FirstOrDefault();

        if (writingReport != null)
        {
            TxtDiagnosticFeedback.Text = writingReport.FeedbackComments;

            try
            {
                using var doc = JsonDocument.Parse(writingReport.DetailedJsonAnalysis);
                if (doc.RootElement.TryGetProperty("ModelAnswerBand8", out var modelAns))
                {
                    TxtModelAnswer.Text = modelAns.GetString() ?? string.Empty;
                }
            }
            catch (JsonException) { /* Dữ liệu cũ không phải JSON hợp lệ */ }
        }

        var speakingReport = summary.Submissions
            .FirstOrDefault(s => s.SkillType == SkillType.Speaking)?.Reports.FirstOrDefault();

        if (speakingReport != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(speakingReport.DetailedJsonAnalysis);
                if (doc.RootElement.TryGetProperty("AdaptiveFollowUpQuestion", out var followUp))
                {
                    TxtAdaptiveQuestion.Text = followUp.GetString() ?? string.Empty;
                }
            }
            catch (JsonException) { /* Dữ liệu cũ không phải JSON hợp lệ */ }
        }
    }

    private static string FormatSkillLine(string label, double? band, string note)
        => band.HasValue ? $"{label}: Band {band.Value:0.0} ({note})" : $"{label}: Chưa hoàn thành";

    private void TxtEssayEditor_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateWordCount();
    }

    private void UpdateWordCount()
    {
        if (TxtEssayEditor == null || TxtWordCount == null) return;
        var words = TxtEssayEditor.Text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        TxtWordCount.Text = $"Số từ: {words.Length} / 250 từ (Khóa phím dán Paste)";
    }

    private void TxtEssayEditor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            e.Handled = true;
            TxtProctoringAlert.Foreground = (SolidColorBrush)FindResource("DangerRed");
            TxtProctoringAlert.Text = "⚠️ BẢO MẬT: Lệnh dán nội dung (Paste) đã bị vô hiệu hóa trong phòng thi!";
        }
    }

    /// <summary>Tìm và tô sáng lần lượt các lần xuất hiện của từ đang chọn trong bài đọc.</summary>
    private void BtnHighlight_Click(object sender, RoutedEventArgs e)
    {
        string keyword = TxtReadingPassage.SelectedText.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            keyword = PromptForText("Nhập từ/cụm từ cần tìm và tô sáng trong bài đọc:", "Công cụ Highlight");
            if (string.IsNullOrWhiteSpace(keyword)) return;
        }

        int startIndex = Math.Max(TxtReadingPassage.SelectionStart + TxtReadingPassage.SelectionLength, 0);
        int found = TxtReadingPassage.Text.IndexOf(keyword, startIndex, StringComparison.OrdinalIgnoreCase);

        // Quay lại từ đầu nếu đã tới cuối bài đọc.
        if (found < 0 && startIndex > 0)
        {
            found = TxtReadingPassage.Text.IndexOf(keyword, 0, StringComparison.OrdinalIgnoreCase);
        }

        if (found < 0)
        {
            TxtActionStatus.Text = $"Không tìm thấy \"{keyword}\" trong bài đọc.";
            return;
        }

        TxtReadingPassage.Focus();
        TxtReadingPassage.Select(found, keyword.Length);
        TxtActionStatus.Text = $"Đã tô sáng \"{keyword}\" tại vị trí {found + 1}.";
    }

    /// <summary>Hộp thoại nhập một dòng văn bản (tránh phụ thuộc Microsoft.VisualBasic).</summary>
    private static string PromptForText(string message, string title)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 460,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            Owner = System.Windows.Application.Current.MainWindow
        };

        var panel = new StackPanel { Margin = new Thickness(20) };

        panel.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var input = new TextBox { Height = 32, FontSize = 13 };
        panel.Children.Add(input);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };

        var ok = new Button { Content = "Tô sáng", Width = 100, Height = 30, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Hủy", Width = 80, Height = 30 };

        ok.Click += (_, _) => { dialog.DialogResult = true; dialog.Close(); };
        cancel.Click += (_, _) => { dialog.DialogResult = false; dialog.Close(); };

        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        dialog.Content = panel;
        dialog.Loaded += (_, _) => input.Focus();

        return dialog.ShowDialog() == true ? input.Text.Trim() : string.Empty;
    }

    private void BtnToggleKiosk_Click(object sender, RoutedEventArgs e)
    {
        _keyboardHook.IsEnabled = !_keyboardHook.IsEnabled;
        if (_keyboardHook.IsEnabled)
        {
            BadgeKiosk.Background = (SolidColorBrush)FindResource("SuccessSoft");
            BadgeKiosk.BorderBrush = (SolidColorBrush)FindResource("SuccessGreen");
            TxtKioskStatus.Text = "🔒 KIOSK LOCK ACTIVE";
            TxtKioskStatus.Foreground = (SolidColorBrush)FindResource("SuccessGreen");
        }
        else
        {
            BadgeKiosk.Background = (SolidColorBrush)FindResource("DangerSoft");
            BadgeKiosk.BorderBrush = (SolidColorBrush)FindResource("DangerRed");
            TxtKioskStatus.Text = "🔓 KIOSK SUSPENDED";
            TxtKioskStatus.Foreground = (SolidColorBrush)FindResource("DangerRed");
        }
    }

    private void BtnExitApp_Click(object sender, RoutedEventArgs e)
    {
        var res = MessageBox.Show("Bạn có chắc chắn muốn thoát khỏi phòng thi Kiosk?", "Xác nhận thoát", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == MessageBoxResult.Yes)
        {
            _recorder.Dispose();
            _audioPlayer.Close();
            _keyboardHook.Dispose();
            _ = _realtime.DisposeAsync();
            _examManager.Dispose();
            System.Windows.Application.Current.Shutdown();
        }
    }

    private void BtnRestart_Click(object sender, RoutedEventArgs e)
    {
        ViewResult.Visibility = Visibility.Collapsed;
        ViewLogin.Visibility = Visibility.Visible;
    }

    private void UpdateTimerText()
    {
        int mins = _remainingSeconds / 60;
        int secs = _remainingSeconds % 60;
        TxtTimer.Text = $"{mins:D2}:{secs:D2}";
    }

    private async void ExamTimer_Tick(object? sender, EventArgs e)
    {
        if (_remainingSeconds <= 0) return;

        _remainingSeconds--;

        if (_remainingSeconds % 30 == 0)
        {
            await _examManager.SaveDraftAsync(_currentSkill, CurrentDraftContent(), CurrentDraftAnswers());
        }

        if (_remainingSeconds == 0)
        {
            _examTimer.Stop();
            _autosaveTimer.Stop();
            MessageBox.Show("Đã hết thời gian làm bài. Hệ thống tự động nộp bài của bạn.",
                "Hết giờ", MessageBoxButton.OK, MessageBoxImage.Warning);
            await ShowFinalReportAsync();
            return;
        }

        UpdateTimerText();
    }

    /// <summary>
    /// Autosave thật: ghi bản nháp xuống database mỗi 10 giây.
    /// Đồng thời cập nhật tiến trình audio Listening (chỉ đọc, không cho tua).
    /// </summary>
    private async void AutosaveTimer_Tick(object? sender, EventArgs e)
    {
        if (_audioPlayer.NaturalDuration.HasTimeSpan)
        {
            BarAudioProgress.Value = _audioPlayer.Position.TotalSeconds;
        }

        if (_examManager.CurrentSession == null) return;

        try
        {
            bool saved = await _examManager.SaveDraftAsync(_currentSkill, CurrentDraftContent(), CurrentDraftAnswers());

            TxtProctoringAlert.Foreground = (SolidColorBrush)FindResource(saved ? "SuccessGreen" : "DangerRed");
            TxtProctoringAlert.Text = saved
                ? $"● Tự động lưu nháp (Autosave) thành công lúc {DateTime.Now:HH:mm:ss}"
                : "⚠️ Không lưu được bản nháp (chưa có ca thi hợp lệ).";
        }
        catch (Exception ex)
        {
            TxtProctoringAlert.Foreground = (SolidColorBrush)FindResource("DangerRed");
            TxtProctoringAlert.Text = $"⚠️ Autosave thất bại: {ex.Message}";
        }
    }

    private string? CurrentDraftContent() => _currentSkill switch
    {
        SkillType.Writing => TxtEssayEditor.Text,
        SkillType.Speaking => TxtSpeakingTranscript.Text,
        _ => null
    };

    private Dictionary<Guid, string>? CurrentDraftAnswers()
    {
        if (_currentSkill is not (SkillType.Listening or SkillType.Reading)) return null;
        return _answerGetters.ToDictionary(k => k.Key, v => v.Value());
    }

    /// <summary>Khôi phục bản nháp đã lưu khi chuyển kỹ năng hoặc mở lại ứng dụng.</summary>
    private async Task RestoreDraftAsync(SkillType skill, ExamSectionDto section)
    {
        AutosaveDraftRequestDto? draft;
        try
        {
            draft = await _examManager.GetDraftAsync(skill);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SmartIELTS] Không đọc được bản nháp: {ex.Message}");
            return;
        }

        if (draft == null) return;

        if (skill == SkillType.Writing && !string.IsNullOrWhiteSpace(draft.DraftContent))
        {
            TxtEssayEditor.Text = draft.DraftContent;
        }
        else if (skill == SkillType.Speaking && !string.IsNullOrWhiteSpace(draft.DraftContent))
        {
            TxtSpeakingTranscript.Text = draft.DraftContent;
        }

        if (draft.DraftAnswers == null) return;

        foreach (var pair in draft.DraftAnswers)
        {
            var question = section.Questions.FirstOrDefault(q => q.Id == pair.Key);
            if (question == null) continue;
            if (!_answerGetters.TryGetValue(pair.Key, out var getter)) continue;

            SetAnswer(question, pair.Value);
        }

        TxtActionStatus.Text = $"Đã khôi phục bản nháp {skill} đã lưu trước đó.";
    }

    /// <summary>Gán lại đáp án vào control tương ứng (dùng khi khôi phục bản nháp).</summary>
    private void SetAnswer(QuestionDto question, string answer)
    {
        var container = _currentSkill == SkillType.Listening ? StackListeningQuestions : StackReadingQuestions;

        foreach (var child in container.Children)
        {
            if (child is not Border border || border.Child is not StackPanel stack) continue;

            var titleBlock = stack.Children.OfType<TextBlock>().FirstOrDefault();
            if (titleBlock == null || !titleBlock.Text.StartsWith($"Câu {question.QuestionNumber} ", StringComparison.Ordinal)) continue;

            var textBox = stack.Children.OfType<TextBox>().FirstOrDefault();
            if (textBox != null)
            {
                textBox.Text = answer;
                return;
            }

            foreach (var radio in stack.Children.OfType<RadioButton>())
            {
                if (string.Equals(radio.Tag as string, answer, StringComparison.OrdinalIgnoreCase))
                {
                    radio.IsChecked = true;
                    return;
                }
            }
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _voice.Dispose();
        _recorder.Dispose();
        _audioPlayer.Close();
        _keyboardHook.Dispose();
        _examManager.NotificationBus.ViolationLogged -= OnViolationLogged;
        _examManager.Dispose();
    }

    private void BtnLogout_Click(object sender, RoutedEventArgs e)
    {
        _examTimer.Stop();
        _autosaveTimer.Stop();
        _keyboardHook.Uninstall();
        _recorder.Stop();
        _isTeacherDashboardOpen = false;

        ViewTeacherDashboard.Visibility = Visibility.Collapsed;
        ViewExam.Visibility = Visibility.Collapsed;
        ViewResult.Visibility = Visibility.Collapsed;
        ViewAdminExams.Visibility = Visibility.Collapsed;
        ViewLogin.Visibility = Visibility.Visible;

        TxtCandidateBadge.Text = "Chưa đăng nhập";
        _remainingSeconds = 165 * 60;
        UpdateTimerText();
        BadgeKiosk.Visibility = Visibility.Visible;
        BadgeRealtime.Visibility = Visibility.Collapsed;
        BtnToggleKiosk.Visibility = Visibility.Visible;
        BtnLogout.Visibility = Visibility.Collapsed;
        TxtProctoringAlert.Text = "● Hệ thống sẵn sàng đăng nhập.";
        TxtProctoringAlert.Foreground = (SolidColorBrush)FindResource("SuccessGreen");
    }

    private async void OpenTeacherDashboard(User user)
    {
        _isTeacherDashboardOpen = true;
        ViewLogin.Visibility = Visibility.Collapsed;
        ViewExam.Visibility = Visibility.Collapsed;
        ViewResult.Visibility = Visibility.Collapsed;
        ViewTeacherDashboard.Visibility = Visibility.Visible;

        TxtCandidateBadge.Text = $"Giám thị: {user.FullName} ({user.Username})";
        TxtTimer.Text = "LIVE";
        BadgeKiosk.Visibility = Visibility.Collapsed;
        BadgeRealtime.Visibility = Visibility.Visible;
        BtnToggleKiosk.Visibility = Visibility.Collapsed;
        BtnLogout.Visibility = Visibility.Visible;

        TxtProctoringAlert.Text = "● Đang kết nối trung tâm Giám sát phòng thi & Bàn duyệt điểm.";
        TxtProctoringAlert.Foreground = (SolidColorBrush)FindResource("AccentBlue");

        // Kết nối realtime: nếu API không chạy thì vẫn dùng cảnh báo cục bộ.
        await _realtime.TryConnectAsync(_examManager.ApiBaseUrl);
        TxtRealtimeStatus.Text = _realtime.StatusText;

        // Tab Quản trị đề thi chỉ dành cho Admin.
        BtnDashTabExams.Visibility = user.Role == UserRole.Admin ? Visibility.Visible : Visibility.Collapsed;

        await RefreshDashboardDataAsync();
    }

    private void BtnDashTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tab)
        {
            var active = (SolidColorBrush)FindResource("PrimaryTeal");
            var inactive = Brushes.Transparent;
            var activeText = Brushes.White;
            var inactiveText = (SolidColorBrush)FindResource("BodyText");

            BtnDashTabProctoring.Background = tab == "Proctoring" ? active : inactive;
            BtnDashTabGrading.Background = tab == "Grading" ? active : inactive;
            BtnDashTabExams.Background = tab == "Exams" ? active : inactive;

            BtnDashTabProctoring.Foreground = tab == "Proctoring" ? activeText : inactiveText;
            BtnDashTabGrading.Foreground = tab == "Grading" ? activeText : inactiveText;
            BtnDashTabExams.Foreground = tab == "Exams" ? activeText : inactiveText;

            PanelDashProctoring.Visibility = tab == "Proctoring" ? Visibility.Visible : Visibility.Collapsed;
            PanelDashGrading.Visibility = tab == "Grading" ? Visibility.Visible : Visibility.Collapsed;

            // Tab quản trị đề thi mở một màn hình riêng (không nằm trong dashboard).
            ViewAdminExams.Visibility = tab == "Exams" ? Visibility.Visible : Visibility.Collapsed;
            ViewTeacherDashboard.Visibility = tab == "Exams" ? Visibility.Collapsed : Visibility.Visible;

            if (tab == "Exams") _ = RefreshAdminExamsAsync();
        }
    }

    private void BtnAdminBack_Click(object sender, RoutedEventArgs e)
    {
        ViewAdminExams.Visibility = Visibility.Collapsed;
        ViewTeacherDashboard.Visibility = Visibility.Visible;

        BtnDashTabProctoring.Background = (SolidColorBrush)FindResource("PrimaryTeal");
        BtnDashTabProctoring.Foreground = Brushes.White;

        BtnDashTabExams.Background = Brushes.Transparent;
        BtnDashTabExams.Foreground = (SolidColorBrush)FindResource("BodyText");

        PanelDashProctoring.Visibility = Visibility.Visible;
        PanelDashGrading.Visibility = Visibility.Collapsed;
    }

    private async void BtnRefreshDashboard_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDashboardDataAsync();
        MessageBox.Show("Dữ liệu giám sát phòng thi và bài nộp đã được đồng bộ mới nhất từ Database!", "Đồng bộ thành công", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task RefreshDashboardDataAsync()
    {
        try
        {
            var sessions = await _examManager.GetActiveSessionsAsync();
            var logs = await _examManager.GetProctoringLogsAsync();
            var pendingReviews = await _examManager.GetPendingReviewsAsync();

            TxtKpiSessions.Text = $"{sessions.Count} Ca thi";
            TxtKpiViolations.Text = $"{logs.Count} Cảnh báo";
            TxtKpiPending.Text = $"{pendingReviews.Count} Bài chờ duyệt";
            TxtKpiAvgBand.Text = FormatAverageBand(sessions);

            ListSessions.ItemsSource = sessions;
            ListViolations.ItemsSource = logs;
            ListPendingReviews.ItemsSource = pendingReviews;

            if (pendingReviews.Count > 0)
            {
                ListPendingReviews.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải dữ liệu Dashboard: {ex.Message}", "Lỗi tải dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string FormatAverageBand(IReadOnlyList<ExamSessionItemDto> sessions)
    {
        var scored = sessions.Where(s => s.OverallBand.HasValue).Select(s => s.OverallBand!.Value).ToList();
        return scored.Count == 0 ? "Chưa có điểm" : $"Band {scored.Average():0.0}";
    }

    private void ListPendingReviews_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListPendingReviews.SelectedItem is PendingReviewItemDto item)
        {
            _selectedReview = item;
            TxtReviewCandidateTitle.Text = $"Thí sinh: {item.CandidateName} • Bài thi {item.SkillType}";
            TxtReviewAiProposedBadge.Text = $"AI Đề Xuất: Band {item.ProposedBand:0.0}";
            TxtReviewEssayContent.Text = item.EssayContent;
            TxtReviewAiFeedback.Text = item.AiFeedback;

            SliderTR.Value = item.Criterion1;
            SliderCC.Value = item.Criterion2;
            SliderLR.Value = item.Criterion3;
            SliderGRA.Value = item.Criterion4;

            TxtTeacherFeedback.Text = $"Bài thi {item.SkillType} đạt yêu cầu. Luận điểm chặt chẽ, vốn từ phù hợp. Đã thẩm định và chốt điểm chính thức.";
            RecalculateOfficialBand();
        }
    }

    private void CriteriaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtValTR == null || TxtValCC == null || TxtValLR == null || TxtValGRA == null) return;
        TxtValTR.Text = SliderTR.Value.ToString("0.0");
        TxtValCC.Text = SliderCC.Value.ToString("0.0");
        TxtValLR.Text = SliderLR.Value.ToString("0.0");
        TxtValGRA.Text = SliderGRA.Value.ToString("0.0");
        RecalculateOfficialBand();
    }

    private void RecalculateOfficialBand()
    {
        if (SliderTR == null || SliderCC == null || SliderLR == null || SliderGRA == null || TxtOfficialBandScore == null) return;
        double rawAvg = (SliderTR.Value + SliderCC.Value + SliderLR.Value + SliderGRA.Value) / 4.0;
        double roundedBand = BandScoreCalculator.RoundToHalfBand(rawAvg);
        TxtOfficialBandScore.Text = $"🎯 ĐIỂM BAND CHÍNH THỨC CỦA GIÁO VIÊN: BAND {roundedBand:0.0}";
    }

    private async void BtnApproveScore_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedReview == null)
        {
            MessageBox.Show("Vui lòng chọn một bài thi để duyệt điểm!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        double rawAvg = (SliderTR.Value + SliderCC.Value + SliderLR.Value + SliderGRA.Value) / 4.0;
        double roundedBand = BandScoreCalculator.RoundToHalfBand(rawAvg);

        try
        {
            await _examManager.ApproveScoreAsync(
                _selectedReview.ReportId,
                SliderTR.Value,
                SliderCC.Value,
                SliderLR.Value,
                SliderGRA.Value,
                roundedBand,
                TxtTeacherFeedback.Text.Trim());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể phê duyệt điểm:\n{ex.Message}", "Lỗi phê duyệt", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        MessageBox.Show($"✅ Giám khảo đã phê duyệt điểm chính thức thành công!\nThí sinh: {_selectedReview.CandidateName}\nKỹ năng: {_selectedReview.SkillType}\nĐiểm Band: {roundedBand:0.0}\nOverall Band của ca thi đã được tính lại.",
            "Phê duyệt thành công", MessageBoxButton.OK, MessageBoxImage.Information);

        await RefreshDashboardDataAsync();
    }

    private async void BtnAddManualViolation_Click(object sender, RoutedEventArgs e)
    {
        string desc = TxtManualViolationDesc.Text.Trim();
        if (string.IsNullOrEmpty(desc)) return;

        await _examManager.AddManualViolationLogAsync("student", desc);
        MessageBox.Show("Đã lập biên bản vi phạm Kiosk cho thí sinh thành công và lưu vào CSDL!", "Biên bản giám thị", MessageBoxButton.OK, MessageBoxImage.Information);
        await RefreshDashboardDataAsync();
    }

    private void ListSessions_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListSessions.SelectedItem is not ExamSessionItemDto session)
        {
            _selectedSession = null;
            TxtSelectedSession.Text = "Chưa chọn ca thi (bấm vào danh sách bên trái)";
            BtnTerminateSession.IsEnabled = false;
            return;
        }

        _selectedSession = session;
        bool canTerminate = !string.Equals(session.Status, SessionStatus.Terminated.ToString(), StringComparison.OrdinalIgnoreCase);

        TxtSelectedSession.Text = $"Ca thi: {session.CandidateName} • {session.Status} • {session.ViolationCount} vi phạm";
        BtnTerminateSession.IsEnabled = canTerminate;
        BtnTerminateSession.Content = canTerminate ? "⛔ Đình Chỉ Ca Thi" : "⛔ Đã đình chỉ";
    }

    private async void BtnTerminateSession_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSession == null)
        {
            MessageBox.Show("Vui lòng chọn một ca thi trong danh sách bên trái.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Đình chỉ ca thi của thí sinh:\n{_selectedSession.CandidateName} ({_selectedSession.Username})\n\n" +
            "Thí sinh sẽ KHÔNG thể nộp bài nữa và ca thi bị khóa vĩnh viễn.\nBạn chắc chắn?",
            "Xác nhận đình chỉ ca thi", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        string reason = TxtManualViolationDesc.Text.Trim();
        if (reason.Length < 5) reason = "Vi phạm quy chế phòng thi";
        if (reason.Length > 500) reason = reason[..500];

        try
        {
            var result = await _examManager.TerminateSessionAsync(_selectedSession.SessionId, reason);
            if (result == null)
            {
                MessageBox.Show("Không tìm thấy ca thi để đình chỉ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string partial = result.PartialOverallBand.HasValue
                ? $"\nSố kỹ năng đã nộp: {result.SubmittedSkillCount}\nĐiểm tạm tính (không chính thức): {result.PartialOverallBand:0.00}"
                : "\nThí sinh chưa nộp kỹ năng nào.";

            MessageBox.Show(
                $"⛔ ĐÃ ĐÌNH CHỈ CA THI\nThí sinh: {result.CandidateName}\nThời điểm: {result.TerminatedAt:HH:mm:ss dd/MM/yyyy}\nLý do: {result.Reason}{partial}",
                "Đình chỉ thành công", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể đình chỉ ca thi:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        await RefreshDashboardDataAsync();
    }

    // =====================================================================
    // ADMIN: QUẢN TRỊ ĐỀ THI
    // =====================================================================

    private async void BtnAdminRefresh_Click(object sender, RoutedEventArgs e) => await RefreshAdminExamsAsync();

    private async Task RefreshAdminExamsAsync()
    {
        try
        {
            var exams = await _examManager.GetExamsForAdminAsync();
            ListAdminExams.ItemsSource = exams;

            // Giữ lựa chọn cũ nếu còn tồn tại.
            if (_selectedAdminExam != null)
            {
                var keep = exams.FirstOrDefault(x => x.Id == _selectedAdminExam.Id);
                if (keep != null)
                {
                    ListAdminExams.SelectedItem = keep;
                    return;
                }
            }

            _selectedAdminExam = null;
            _selectedAdminSection = null;
            _adminSections = new List<ExamSectionAdminDto>();
            ListAdminSections.ItemsSource = null;
            ListAdminQuestions.ItemsSource = null;
            TxtAdminExamTitle.Text = "Chưa chọn đề thi";
            UpdateAdminButtons();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được danh sách đề thi:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ListAdminExams_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListAdminExams.SelectedItem is not ExamAdminDto exam)
        {
            _selectedAdminExam = null;
            TxtAdminExamTitle.Text = "Chưa chọn đề thi";
            _adminSections = new List<ExamSectionAdminDto>();
            ListAdminSections.ItemsSource = null;
            ListAdminQuestions.ItemsSource = null;
            UpdateAdminButtons();
            return;
        }

        _selectedAdminExam = exam;
        _adminSections = exam.Sections.ToList();

        string activeLabel = exam.IsActive ? "ĐANG HOẠT ĐỘNG" : "ĐÃ TẮT";
        TxtAdminExamTitle.Text = $"{exam.Title}  •  {exam.SectionCount} phần / {exam.QuestionCount} câu / {exam.SessionCount} ca thi  •  {activeLabel}";

        ListAdminSections.ItemsSource = _adminSections.Select(DescribeSection).ToList();
        ListAdminSections.SelectedIndex = _adminSections.Count > 0 ? 0 : -1;
        UpdateAdminButtons();
    }

    private static string DescribeSection(ExamSectionAdminDto section)
        => $"{section.SectionOrder}. {section.SkillType} — {section.DurationMinutes} phút — {section.Questions.Count} câu";

    private void ListAdminSections_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int index = ListAdminSections.SelectedIndex;
        if (index < 0 || index >= _adminSections.Count)
        {
            _selectedAdminSection = null;
            ListAdminQuestions.ItemsSource = null;
            UpdateAdminButtons();
            return;
        }

        _selectedAdminSection = _adminSections[index];
        ListAdminQuestions.ItemsSource = _selectedAdminSection.Questions;
        UpdateAdminButtons();
    }

    private void ListAdminQuestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdateAdminButtons();

    private void UpdateAdminButtons()
    {
        BtnAdminAddSection.IsEnabled = _selectedAdminExam != null;
        BtnAdminDeleteSection.IsEnabled = _selectedAdminSection != null;
        BtnAdminAddQuestion.IsEnabled = _selectedAdminSection != null;
        BtnAdminDeleteQuestion.IsEnabled = ListAdminQuestions.SelectedItem is QuestionAdminDto;
    }

    private async void BtnAdminCreateExam_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new FormDialog("Tạo đề thi mới", 520);
        dialog.AddText("title", "Tiêu đề đề thi *", "IELTS Academic Mock Test mới");
        dialog.AddMultiline("description", "Mô tả", "Bài thi thử 4 kỹ năng.");
        dialog.AddEnum("examType", "Loại đề thi", ExamType.Academic);
        dialog.AddText("duration", "Tổng thời lượng (phút)", "165");

        if (dialog.ShowDialog() != true) return;

        try
        {
            var created = await _examManager.CreateExamAsync(new CreateExamRequestDto
            {
                Title = dialog.Get("title"),
                Description = dialog.Get("description"),
                ExamType = dialog.GetEnum("examType", ExamType.Academic),
                TotalDurationMinutes = dialog.GetInt("duration", 165),
                IsActive = true
            });

            MessageBox.Show($"Đã tạo đề thi:\n{created?.Title}\n\nBước tiếp theo: thêm 4 phần thi và câu hỏi.",
                "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tạo được đề thi:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        await RefreshAdminExamsAsync();
    }

    private async void BtnAdminDeleteExam_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedAdminExam == null)
        {
            MessageBox.Show("Vui lòng chọn một đề thi.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_selectedAdminExam.SessionCount > 0)
        {
            MessageBox.Show(
                $"Không thể xóa: đề thi đã có {_selectedAdminExam.SessionCount} ca thi.\n\n" +
                "Để ngừng sử dụng, hãy sửa đề và đặt IsActive = false (giữ lại dữ liệu điểm).",
                "Không thể xóa", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show($"Xóa đề thi \"{_selectedAdminExam.Title}\"?\nToàn bộ phần thi và câu hỏi cũng bị xóa.",
                "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        bool deleted = await _examManager.DeleteExamAsync(_selectedAdminExam.Id);
        if (!deleted)
        {
            MessageBox.Show("Không xóa được đề thi (đã có ca thi hoặc lỗi dữ liệu).", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _selectedAdminExam = null;
        await RefreshAdminExamsAsync();
    }

    private async void BtnAdminAddSection_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedAdminExam == null) return;

        // Gợi ý kỹ năng còn thiếu, ưu tiên đúng thứ tự Listening -> Reading -> Writing -> Speaking.
        var used = _adminSections.Select(s => s.SkillType).ToHashSet();
        var next = new[] { SkillType.Listening, SkillType.Reading, SkillType.Writing, SkillType.Speaking }
            .FirstOrDefault(s => !used.Contains(s));

        if (used.Count >= 4)
        {
            MessageBox.Show("Đề thi đã có đủ 4 phần thi (mỗi kỹ năng một phần).", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new FormDialog("Thêm phần thi", 480);
        dialog.AddEnum("skill", "Kỹ năng", next);
        dialog.AddText("order", "Thứ tự phần (1-4)", (used.Count + 1).ToString());
        dialog.AddText("duration", "Thời lượng (phút)", "30");
        dialog.AddText("media", "MediaUrl (audio/video, có thể bỏ trống)", "");
        dialog.AddMultiline("passage", "Nội dung / PassageText / Đề bài", "");

        if (dialog.ShowDialog() != true) return;

        try
        {
            await _examManager.AddSectionAsync(_selectedAdminExam.Id, new CreateSectionRequestDto
            {
                SkillType = dialog.GetEnum("skill", next),
                SectionOrder = dialog.GetInt("order", used.Count + 1),
                DurationMinutes = dialog.GetInt("duration", 30),
                MediaUrl = dialog.Get("media"),
                PassageText = dialog.Get("passage")
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thêm được phần thi:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        await RefreshAdminExamsAsync();
    }

    private async void BtnAdminDeleteSection_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedAdminExam == null || _selectedAdminSection == null) return;

        if (MessageBox.Show($"Xóa phần thi {_selectedAdminSection.SkillType} và toàn bộ câu hỏi của nó?",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        await _examManager.DeleteSectionAsync(_selectedAdminSection.Id);
        _selectedAdminSection = null;
        await RefreshAdminExamsAsync();
    }

    private async void BtnAdminAddQuestion_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedAdminExam == null || _selectedAdminSection == null) return;

        int nextNumber = _selectedAdminSection.Questions.Count == 0
            ? 1
            : _selectedAdminSection.Questions.Max(q => q.QuestionNumber) + 1;

        var dialog = new FormDialog("Thêm câu hỏi", 620);
        dialog.AddText("number", "Số thứ tự câu *", nextNumber.ToString());
        dialog.AddEnum("type", "Loại câu hỏi", QuestionType.FillBlank);
        dialog.AddMultiline("text", "Nội dung câu hỏi *", "", 80);
        dialog.AddText("options", "OptionsJson (chỉ câu trắc nghiệm)", "");
        dialog.AddText("answer", "Đáp án đúng * (A / B để cho nhiều biến thể)", "");
        dialog.AddText("score", "Điểm tối đa", "1");

        if (dialog.ShowDialog() != true) return;

        try
        {
            await _examManager.AddQuestionAsync(_selectedAdminSection.Id, new CreateQuestionRequestDto
            {
                QuestionNumber = dialog.GetInt("number", nextNumber),
                QuestionType = dialog.GetEnum("type", QuestionType.FillBlank),
                QuestionText = dialog.Get("text"),
                OptionsJson = string.IsNullOrWhiteSpace(dialog.Get("options")) ? null : dialog.Get("options"),
                CorrectAnswer = dialog.Get("answer"),
                MaxScore = dialog.GetDouble("score", 1.0)
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thêm được câu hỏi:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        await RefreshAdminExamsAsync();
    }

    private async void BtnAdminDeleteQuestion_Click(object sender, RoutedEventArgs e)
    {
        if (ListAdminQuestions.SelectedItem is not QuestionAdminDto question) return;

        if (MessageBox.Show($"Xóa câu hỏi số {question.QuestionNumber}?", "Xác nhận",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        await _examManager.DeleteQuestionAsync(question.Id);
        await RefreshAdminExamsAsync();
    }

    /// <summary>Nút hỗ trợ demo: điền sẵn đáp án mẫu (không dùng trong thi thật).</summary>
    private void BtnFillDemoAnswers_Click(object sender, RoutedEventArgs e)
    {
        var section = _examManager.CurrentExam?.Sections.FirstOrDefault(s => s.SkillType == _currentSkill);
        if (section == null) return;

        if (_currentSkill == SkillType.Listening)
        {
            foreach (var q in section.Questions)
            {
                string demoAnswer = q.QuestionNumber switch
                {
                    1 => "Anderson",
                    2 => "25 October",
                    3 => "three",
                    4 => "Credit Card",
                    5 => "color",
                    _ => string.Empty
                };
                if (demoAnswer.Length > 0) SetAnswer(q, demoAnswer);
            }
            TxtActionStatus.Text = "● Đã điền sẵn bộ đáp án mẫu chuẩn cho phần Listening (Demo).";
        }
        else if (_currentSkill == SkillType.Reading)
        {
            foreach (var q in section.Questions)
            {
                string demoAnswer = q.QuestionNumber switch
                {
                    1 => "True",
                    2 => "False",
                    3 => "Not Given",
                    4 => "High-performance GPUs and massive web-scale datasets",
                    5 => "raw data",
                    _ => string.Empty
                };
                if (demoAnswer.Length > 0) SetAnswer(q, demoAnswer);
            }
            TxtActionStatus.Text = "● Đã điền sẵn bộ đáp án mẫu chuẩn cho phần Reading (Demo).";
        }
        else if (_currentSkill == SkillType.Writing)
        {
            TxtEssayEditor.Text = DemoEssayText;
            UpdateWordCount();
            TxtActionStatus.Text = "● Đã điền sẵn bài luận mẫu cho phần Writing (Demo).";
        }
    }

    private const string DemoEssayText =
        "In contemporary society, artificial intelligence has emerged as a transformative force across numerous sectors, including education. " +
        "While some commentators argue that automated systems will completely supplant human educators, it is my firm conviction that teachers play an " +
        "indispensable, multifaceted role that technology can only augment rather than extinguish.\n\n" +
        "First and foremost, human teachers provide emotional intelligence, empathetic mentoring, and moral guidance that algorithmic systems fundamentally lack. " +
        "Furthermore, pedagogy is not merely the mechanical transmission of raw information; it requires profound understanding of diverse student temperaments " +
        "and motivational psychology. Consequently, teachers serve as an essential catalyst for holistic human development.\n\n" +
        "In conclusion, although artificial intelligence offers unprecedented personalized learning materials, it will never replace the profound bond and " +
        "moral inspiration provided by human instructors.";
}
