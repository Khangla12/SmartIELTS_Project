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
    Published = 4
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
    HotkeyBlocked = 5
}
