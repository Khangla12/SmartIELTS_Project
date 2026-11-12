using SmartIELTS.Application.DTOs;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Infrastructure.Services;

internal static class DtoMapper
{
    public static ExamDto ToExamDto(Exam exam) => new()
    {
        Id = exam.Id,
        Title = exam.Title,
        Description = exam.Description,
        ExamType = exam.ExamType,
        TotalDurationMinutes = exam.TotalDurationMinutes,
        Sections = exam.Sections
            .OrderBy(s => s.SectionOrder)
            .Select(ToSectionDto)
            .ToList()
    };

    public static ExamSectionDto ToSectionDto(ExamSection section) => new()
    {
        Id = section.Id,
        SkillType = section.SkillType,
        SectionOrder = section.SectionOrder,
        DurationMinutes = section.DurationMinutes,
        MediaUrl = section.MediaUrl,
        PassageText = section.PassageText,
        // Không trả CorrectAnswer ra ngoài: DTO cố tình không chứa trường này.
        Questions = section.Questions
            .OrderBy(q => q.QuestionNumber)
            .Select(q => new QuestionDto
            {
                Id = q.Id,
                QuestionNumber = q.QuestionNumber,
                QuestionType = q.QuestionType,
                QuestionText = q.QuestionText,
                OptionsJson = q.OptionsJson,
                MaxScore = q.MaxScore
            })
            .ToList()
    };

    /// <summary>
    /// Chuyển ca thi thành DTO an toàn cho THÍ SINH.
    /// <para>
    /// QUY TẮC: AI chỉ hỗ trợ giảng viên. Thí sinh chỉ thấy điểm sau khi giảng viên
    /// duyệt. Mọi dữ liệu AI (điểm tiêu chí, nhận xét, JSON phân tích, bài mẫu)
    /// bị loại bỏ hoàn toàn ở đây — không chỉ ẩn trên UI.
    /// </para>
    /// </summary>
    public static SessionResultDto ToSessionResultDto(ExamSession session)
        => BuildResult(session, includeAiData: false);

    /// <summary>
    /// Bản đầy đủ dành cho GIẢNG VIÊN / ADMIN: bao gồm điểm và nhận xét của AI
    /// để phục vụ việc thẩm định.
    /// </summary>
    public static SessionResultDto ToSessionResultDtoForStaff(ExamSession session)
        => BuildResult(session, includeAiData: true);

    private static SessionResultDto BuildResult(ExamSession session, bool includeAiData)
    {
        var skillViews = session.Submissions
            .Select(sub => ToSkillDto(sub, includeAiData))
            .OrderBy(s => s.SkillType)
            .ToList();

        // "Đã duyệt đầy đủ" chỉ đúng khi ĐÃ NỘP ĐỦ 4 KỸ NĂNG và tất cả đều được duyệt.
        bool hasAllFourSkills = skillViews.Count == 4;
        bool allApproved = hasAllFourSkills && skillViews.All(s => s.IsApproved);
        int pendingCount = skillViews.Count(s => !s.IsApproved);

        // ---------------------------------------------------------------------
        // QUY TẮC: THÍ SINH KHÔNG THẤY BẤT KỲ ĐIỂM NÀO.
        // Chỉ khi ĐỦ 4 KỸ NĂNG và TẤT CẢ đã được giảng viên duyệt thì mới công bố
        // điểm (kể cả Listening/Reading đã chấm tự động).
        // Giảng viên/Admin (includeAiData = true) luôn thấy đầy đủ.
        // ---------------------------------------------------------------------
        bool canPublishScores = includeAiData || allApproved;

        double? BandOf(SkillType skill)
        {
            var view = skillViews.FirstOrDefault(s => s.SkillType == skill);
            if (view == null) return null;
            return canPublishScores ? view.BandScore : null;
        }

        var l = BandOf(SkillType.Listening);
        var r = BandOf(SkillType.Reading);
        var w = BandOf(SkillType.Writing);
        var sp = BandOf(SkillType.Speaking);

        var result = new SessionResultDto
        {
            SessionId = session.Id,
            CandidateName = session.User?.FullName ?? "Thí sinh",
            ExamTitle = session.Exam?.Title ?? "Kỳ thi IELTS",
            Status = session.Status,
            IsFullyApproved = allApproved,
            PendingReviewCount = pendingCount,
            ListeningBand = l,
            ReadingBand = r,
            WritingBand = w,
            SpeakingBand = sp,
            Submissions = canPublishScores
                ? skillViews
                : skillViews.Select(HideScores).ToList()
        };

        // Overall Band: chỉ công bố khi MỌI kỹ năng đã được duyệt.
        if (allApproved && l.HasValue && r.HasValue && w.HasValue && sp.HasValue)
        {
            result.OverallBandScore = session.OverallBandScore
                ?? Domain.Common.BandScoreCalculator.CalculateOverallBand(l.Value, r.Value, w.Value, sp.Value);
        }

        result.StatusMessage = BuildStatusMessage(skillViews, allApproved, pendingCount, includeAiData);
        return result;
    }

    /// <summary>
    /// Bản sao của một kỹ năng đã xoá sạch điểm — dùng khi chưa đủ điều kiện công bố.
    /// </summary>
    private static SkillSubmissionDto HideScores(SkillSubmissionDto source) => new()
    {
        SkillType = source.SkillType,
        RawScore = null,
        BandScore = null,
        EssayContent = source.EssayContent,
        IsApproved = source.IsApproved,
        IsPendingReview = source.IsPendingReview,
        ApprovedAt = source.ApprovedAt,
        TeacherFeedback = null,
        // Không trả báo cáo nào (chứa điểm tiêu chí + nhận xét AI).
        Reports = new List<EvaluationReportDto>()
    };

    private static string BuildStatusMessage(
        List<SkillSubmissionDto> skills, bool allApproved, int pendingCount, bool includeAiData)
    {
        if (skills.Count == 0)
            return "Bạn chưa nộp kỹ năng nào.";

        if (allApproved && skills.Count == 4)
            return "Bài thi đã được giảng viên chấm và xác nhận. Điểm dưới đây là điểm chính thức.";

        if (includeAiData)
            return $"{pendingCount} kỹ năng đang chờ duyệt điểm.";

        // Chưa nộp đủ 4 kỹ năng
        if (skills.Count < 4)
        {
            string submitted = string.Join(", ", skills.Select(s => SkillName(s.SkillType)));
            return $"Bạn đã nộp {skills.Count}/4 kỹ năng ({submitted}). " +
                   "Hãy hoàn thành các phần còn lại để được chốt Overall Band.";
        }

        var pendingNames = skills
            .Where(s => !s.IsApproved)
            .Select(s => SkillName(s.SkillType))
            .ToList();

        return $"Bài làm đã được ghi nhận. Phần {string.Join(", ", pendingNames)} đang chờ giảng viên chấm và xác nhận điểm. " +
               "Điểm chính thức chỉ hiển thị sau khi giảng viên duyệt.";
    }

    private static string SkillName(SkillType skill) => skill switch
    {
        SkillType.Listening => "Listening",
        SkillType.Reading => "Reading",
        SkillType.Writing => "Writing",
        SkillType.Speaking => "Speaking",
        _ => skill.ToString()
    };

    /// <summary>Bản CÔNG KHAI cho thí sinh: ẩn điểm/nhận xét AI, chỉ hiện khi đã duyệt.</summary>
    public static SkillSubmissionDto ToSkillDtoPublic(CandidateSubmission submission)
        => ToSkillDto(submission, includeAiData: false);

    /// <summary>
    /// Chuyển một bài nộp. Khi <paramref name="includeAiData"/> = false (thí sinh):
    /// - Điểm (RawScore/BandScore) chỉ trả khi đã duyệt
    /// - KHÔNG trả điểm tiêu chí, nhận xét AI, JSON phân tích, bài mẫu
    /// </summary>
    private static SkillSubmissionDto ToSkillDto(CandidateSubmission submission, bool includeAiData)
    {
        bool approved = submission.EvaluationReports.Any(r => r.IsApproved);
        var approvedReport = submission.EvaluationReports.FirstOrDefault(r => r.IsApproved);

        var dto = new SkillSubmissionDto
        {
            SkillType = submission.SkillType,
            EssayContent = submission.EssayContent,
            IsApproved = approved,
            IsPendingReview = !approved,
            ApprovedAt = approvedReport?.ApprovedAt,
            TeacherFeedback = approvedReport?.TeacherFeedback
        };

        // Điểm: thí sinh chỉ thấy khi đã duyệt; giảng viên thấy cả điểm tạm của AI.
        if (approved || includeAiData)
        {
            dto.RawScore = submission.RawScore;
            dto.BandScore = submission.BandScore;
        }

        if (!includeAiData)
        {
            // THÍ SINH: chỉ trả những trường an toàn, không có dữ liệu AI.
            if (approved)
            {
                dto.Reports = submission.EvaluationReports
                    .Where(r => r.IsApproved)
                    .Select(r => new EvaluationReportDto
                    {
                        Id = r.Id,
                        EvaluatedBy = EvaluatorType.Teacher,
                        IsApproved = true,
                        ApprovedAt = r.ApprovedAt,
                        TeacherFeedback = r.TeacherFeedback,
                        // Điểm 4 tiêu chí do giảng viên chốt thì được phép công bố.
                        CriterionScore1 = r.CriterionScore1,
                        CriterionScore2 = r.CriterionScore2,
                        CriterionScore3 = r.CriterionScore3,
                        CriterionScore4 = r.CriterionScore4
                        // FeedbackComments / DetailedJsonAnalysis: CỐ TÌNH BỎ TRỐNG
                    })
                    .ToList();
            }

            return dto;
        }

        // GIẢNG VIÊN / ADMIN: trả đầy đủ để thẩm định.
        dto.Reports = submission.EvaluationReports
            .Select(r => new EvaluationReportDto
            {
                Id = r.Id,
                EvaluatedBy = r.EvaluatedBy,
                CriterionScore1 = r.CriterionScore1,
                CriterionScore2 = r.CriterionScore2,
                CriterionScore3 = r.CriterionScore3,
                CriterionScore4 = r.CriterionScore4,
                FeedbackComments = r.FeedbackComments,
                DetailedJsonAnalysis = r.DetailedJsonAnalysis,
                TeacherFeedback = r.TeacherFeedback,
                IsApproved = r.IsApproved,
                ApprovedAt = r.ApprovedAt
            })
            .ToList();

        return dto;
    }
}
