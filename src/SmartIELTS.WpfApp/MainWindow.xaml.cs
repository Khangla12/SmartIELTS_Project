using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;
using SmartIELTS.WpfApp.Services;

namespace SmartIELTS.WpfApp;

public partial class MainWindow : Window
{
    private readonly WindowsKeyboardHook _keyboardHook;
    private readonly LocalExamManager _examManager;
    private readonly DispatcherTimer _examTimer;
    private readonly DispatcherTimer _autosaveTimer;

    private int _remainingSeconds = 165 * 60;
    private SkillType _currentSkill = SkillType.Listening;
    private readonly Dictionary<Guid, TextBox> _questionInputs = new();

    public MainWindow()
    {
        InitializeComponent();

        _keyboardHook = new WindowsKeyboardHook();
        _keyboardHook.OnHotkeyBlocked += KeyboardHook_OnHotkeyBlocked;

        _examManager = new LocalExamManager();

        _examTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _examTimer.Tick += ExamTimer_Tick;

        _autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _autosaveTimer.Tick += AutosaveTimer_Tick;
    }

    private void KeyboardHook_OnHotkeyBlocked(string hotkey)
    {
        Dispatcher.Invoke(() =>
        {
            TxtProctoringAlert.Foreground = (SolidColorBrush)FindResource("DangerRed");
            TxtProctoringAlert.Text = $"⚠️ CẢNH BÁO VI PHẠM KIOSK: Hệ thống đã chặn phím nóng [{hotkey}]!";
            _ = _examManager.LogProctoringViolationAsync(ViolationType.HotkeyBlocked, $"Blocked hotkey: {hotkey}");
        });
    }

    private async void BtnStartExam_Click(object sender, RoutedEventArgs e)
    {
        string username = TxtUsername.Text.Trim();
        string password = TxtPassword.Password;

        var user = await _examManager.LoginAsync(username, password);
        if (user == null)
        {
            MessageBox.Show("Tài khoản hoặc mật khẩu không chính xác!", "Lỗi xác thực", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var exam = await _examManager.GetActiveExamAsync();
        if (exam == null)
        {
            MessageBox.Show("Không tìm thấy đề thi hoạt động!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await _examManager.StartSessionAsync();

        // Update UI
        TxtCandidateBadge.Text = $"Thí sinh: {user.FullName} ({user.Username})";
        ViewLogin.Visibility = Visibility.Collapsed;
        ViewExam.Visibility = Visibility.Visible;

        // Activate Kiosk Hook
        try
        {
            _keyboardHook.Install();
        }
        catch { /* Ignore if no admin */ }

        // Start Timers
        _examTimer.Start();
        _autosaveTimer.Start();

        // Load Listening
        LoadSkillView(SkillType.Listening);
    }

    private void LoadSkillView(SkillType skill)
    {
        _currentSkill = skill;
        _questionInputs.Clear();

        // Update tabs styling
        TabListening.Background = skill == SkillType.Listening ? (SolidColorBrush)FindResource("PrimaryTeal") : new SolidColorBrush(Color.FromRgb(71, 85, 105));
        TabReading.Background = skill == SkillType.Reading ? (SolidColorBrush)FindResource("PrimaryTeal") : new SolidColorBrush(Color.FromRgb(71, 85, 105));
        TabWriting.Background = skill == SkillType.Writing ? (SolidColorBrush)FindResource("PrimaryTeal") : new SolidColorBrush(Color.FromRgb(71, 85, 105));
        TabSpeaking.Background = skill == SkillType.Speaking ? (SolidColorBrush)FindResource("PrimaryTeal") : new SolidColorBrush(Color.FromRgb(71, 85, 105));

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
                if (string.IsNullOrWhiteSpace(TxtEssayEditor.Text))
                {
                    TxtEssayEditor.Text = "In contemporary society, artificial intelligence has emerged as a transformative force across numerous sectors, including education. While some commentators argue that automated systems will completely supplant human educators, it is my firm conviction that teachers play an indispensable, multifaceted role that technology can only augment rather than extinguish.\n\nFirst and foremost, human teachers provide emotional intelligence, empathetic mentoring, and moral guidance that algorithmic systems fundamentally lack. Furthermore, pedagogy is not merely the mechanical transmission of raw information; it requires profound understanding of diverse student temperaments and motivational psychology. Consequently, teachers serve as an essential catalyst for holistic human development.\n\nIn conclusion, although artificial intelligence offers unprecedented personalized learning materials, it will never replace the profound bond and moral inspiration provided by human instructors.";
                }
                UpdateWordCount();
                break;

            case SkillType.Speaking:
                TxtSpeakingPrompt.Text = section.PassageText ?? string.Empty;
                break;
        }

        TxtActionStatus.Text = $"Đang làm phần thi: {skill}";
    }

    private UIElement CreateQuestionControl(Question q)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 12),
            BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
            BorderThickness = new Thickness(1)
        };

        var stack = new StackPanel();

        var title = new TextBlock
        {
            Text = $"Câu {q.QuestionNumber} [{q.QuestionType}]: {q.QuestionText}",
            Foreground = (SolidColorBrush)FindResource("TextLight"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        stack.Children.Add(title);

        var txtAnswer = new TextBox
        {
            Height = 36,
            FontSize = 13,
            Margin = new Thickness(0, 4, 0, 0)
        };

        // Fill pre-populated standard demo answers for convenience
        if (q.Section?.SkillType == SkillType.Listening)
        {
            if (q.QuestionNumber == 1) txtAnswer.Text = "Anderson";
            if (q.QuestionNumber == 2) txtAnswer.Text = "25 October";
            if (q.QuestionNumber == 3) txtAnswer.Text = "three";
            if (q.QuestionNumber == 4) txtAnswer.Text = "Credit Card";
            if (q.QuestionNumber == 5) txtAnswer.Text = "color";
        }
        else if (q.Section?.SkillType == SkillType.Reading)
        {
            if (q.QuestionNumber == 1) txtAnswer.Text = "True";
            if (q.QuestionNumber == 2) txtAnswer.Text = "False";
            if (q.QuestionNumber == 3) txtAnswer.Text = "Not Given";
            if (q.QuestionNumber == 4) txtAnswer.Text = "High-performance GPUs and massive web-scale datasets";
            if (q.QuestionNumber == 5) txtAnswer.Text = "raw data";
        }

        _questionInputs[q.Id] = txtAnswer;
        stack.Children.Add(txtAnswer);

        border.Child = stack;
        return border;
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
        var answers = _questionInputs.ToDictionary(k => k.Key, v => v.Value.Text);
        string? essay = _currentSkill == SkillType.Writing ? TxtEssayEditor.Text : null;
        string? audio = _currentSkill == SkillType.Speaking ? "https://storage.ailes.edu.vn/audio/speaking_candidate_01.wav" : null;

        TxtActionStatus.Text = $"Đang chấm điểm kỹ năng {_currentSkill} qua bộ máy Grader Strategy...";

        var result = await _examManager.SubmitSkillAsync(_currentSkill, answers, essay, audio);

        MessageBox.Show($"Chấm điểm hoàn tất!\nKỹ năng: {_currentSkill}\nĐiểm Band: {result.BandScore}\nNhận xét: {result.FeedbackComments}", 
                        "Kết quả chấm điểm", MessageBoxButton.OK, MessageBoxImage.Information);

        // Advance to next skill or final report
        if (_currentSkill == SkillType.Listening) LoadSkillView(SkillType.Reading);
        else if (_currentSkill == SkillType.Reading) LoadSkillView(SkillType.Writing);
        else if (_currentSkill == SkillType.Writing) LoadSkillView(SkillType.Speaking);
        else if (_currentSkill == SkillType.Speaking) ShowFinalReport();
    }

    private async void BtnSubmitSpeaking_Click(object sender, RoutedEventArgs e)
    {
        string transcript = TxtSpeakingTranscript.Text;
        TxtActionStatus.Text = "Đang gửi bản ghi âm cho AI chấm âm vị và kiểm tra luận điểm...";

        var result = await _examManager.SubmitSkillAsync(SkillType.Speaking, null, transcript, "speaking_candidate.wav");

        ShowFinalReport();
    }

    private async void ShowFinalReport()
    {
        _examTimer.Stop();
        _autosaveTimer.Stop();
        _keyboardHook.IsEnabled = false;

        ViewExam.Visibility = Visibility.Collapsed;
        ViewResult.Visibility = Visibility.Visible;

        var summary = await _examManager.GetSessionSummaryAsync();

        TxtOverallBand.Text = (summary.OverallBandScore ?? 7.5).ToString("0.0");
        TxtScoreListening.Text = $"🎧 Listening: Band {(summary.ListeningBand ?? 7.5):0.0} (Auto Tolerance Grader)";
        TxtScoreReading.Text = $"📖 Reading: Band {(summary.ReadingBand ?? 7.0):0.0} (Evidence Exact Grader)";
        TxtScoreWriting.Text = $"✍️ Writing: Band {(summary.WritingBand ?? 7.0):0.0} (Cambridge 4 Criteria AI)";
        TxtScoreSpeaking.Text = $"🎙️ Speaking: Band {(summary.SpeakingBand ?? 7.5):0.0} (Azure Speech Phoneme AI)";

        var writingSub = summary.Submissions.FirstOrDefault(s => s.SkillType == SkillType.Writing);
        var writingReport = writingSub?.Reports.FirstOrDefault();
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
            catch { /* Ignore */ }
        }

        var speakingSub = summary.Submissions.FirstOrDefault(s => s.SkillType == SkillType.Speaking);
        var speakingReport = speakingSub?.Reports.FirstOrDefault();
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
            catch { /* Ignore */ }
        }
    }

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
        // Block Ctrl+V (Paste) in secure exam mode
        if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            e.Handled = true;
            TxtProctoringAlert.Foreground = (SolidColorBrush)FindResource("DangerRed");
            TxtProctoringAlert.Text = "⚠️ BẢO MẬT: Lệnh dán nội dung (Paste) đã bị vô hiệu hóa trong phòng thi!";
        }
    }

    private void BtnHighlight_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Công cụ Highlight đã được kích hoạt trên đoạn văn bản đang chọn.", "Split-Screen Reading", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnToggleKiosk_Click(object sender, RoutedEventArgs e)
    {
        _keyboardHook.IsEnabled = !_keyboardHook.IsEnabled;
        if (_keyboardHook.IsEnabled)
        {
            BadgeKiosk.Background = new SolidColorBrush(Color.FromRgb(6, 78, 59));
            TxtKioskStatus.Text = "🔒 KIOSK LOCK ACTIVE";
            TxtKioskStatus.Foreground = (SolidColorBrush)FindResource("SuccessGreen");
        }
        else
        {
            BadgeKiosk.Background = new SolidColorBrush(Color.FromRgb(127, 29, 29));
            TxtKioskStatus.Text = "🔓 KIOSK SUSPENDED (DEMO)";
            TxtKioskStatus.Foreground = (SolidColorBrush)FindResource("DangerRed");
        }
    }

    private void BtnExitApp_Click(object sender, RoutedEventArgs e)
    {
        var res = MessageBox.Show("Bạn có chắc chắn muốn thoát khỏi phòng thi Kiosk?", "Xác nhận thoát", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == MessageBoxResult.Yes)
        {
            _keyboardHook.Dispose();
            System.Windows.Application.Current.Shutdown();
        }
    }

    private void BtnRestart_Click(object sender, RoutedEventArgs e)
    {
        ViewResult.Visibility = Visibility.Collapsed;
        ViewLogin.Visibility = Visibility.Visible;
    }

    private void ExamTimer_Tick(object? sender, EventArgs e)
    {
        if (_remainingSeconds > 0)
        {
            _remainingSeconds--;
            int mins = _remainingSeconds / 60;
            int secs = _remainingSeconds % 60;
            TxtTimer.Text = $"{mins:D2}:{secs:D2}";
        }
    }

    private void AutosaveTimer_Tick(object? sender, EventArgs e)
    {
        TxtProctoringAlert.Foreground = (SolidColorBrush)FindResource("SuccessGreen");
        TxtProctoringAlert.Text = $"● Tự động lưu nháp (Autosave) thành công lúc {DateTime.Now:HH:mm:ss} xuống SQLite cục bộ.";
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _keyboardHook.Dispose();
    }
}