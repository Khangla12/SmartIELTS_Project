namespace SmartIELTS.Domain.Enums;

public enum UserRole
{
    Candidate = 1,
    Teacher = 2,
    Admin = 3
}

public enum ExamType
{
    Academic = 1,
    GeneralTraining = 2
}

public enum SkillType
{
    Listening = 1,
    Reading = 2,
    Writing = 3,
    Speaking = 4
}

public enum QuestionType
{
    MultipleChoice = 1,
    FillBlank = 2,
    TrueFalseNotGiven = 3,
    Matching = 4,
    Essay = 5,
    SpeakingPrompt = 6
}

public enum SessionStatus
{
    InProgress = 1,
    Submitted = 2,
    Evaluated = 3,
    Published = 4,
    /// <summary>Ca thi bị giám thị đình chỉ (vi phạm quy chế); không nhận bài nộp nữa.</summary>
    Terminated = 5
}

public enum EvaluatorType
{
    AiEngine = 1,
    Teacher = 2
}

public enum ViolationType
{
    TabSwitch = 1,
    FaceAbsent = 2,
    MultipleFaces = 3,
    NoiseDetected = 4,
    HotkeyBlocked = 5,
    /// <summary>Giám thị đình chỉ ca thi.</summary>
    SessionTerminated = 6
}

/// <summary>Các phần của bài thi IELTS Speaking.</summary>
public enum SpeakingPart
{
    /// <summary>Part 1: Introduction & Interview (câu hỏi quen thuộc).</summary>
    Part1Introduction = 1,
    /// <summary>Part 2: Long turn (cue card + 1 phút chuẩn bị + 2 phút nói).</summary>
    Part2LongTurn = 2,
    /// <summary>Part 3: Two-way discussion (câu hỏi phản biện, thích ứng).</summary>
    Part3Discussion = 3
}
