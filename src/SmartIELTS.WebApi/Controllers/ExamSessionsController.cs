using System.Text.Json;
using SmartIELTS.Application.DTOs;
using SmartIELTS.Application.GradingStrategies;
using SmartIELTS.Domain.Common;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;
using SmartIELTS.Domain.Interfaces;
using SmartIELTS.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartIELTS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExamSessionsController : ControllerBase
{
    private readonly SmartIeltsDbContext _context;
    private readonly IGradingStrategyFactory _gradingFactory;

    public ExamSessionsController(SmartIeltsDbContext context, IGradingStrategyFactory gradingFactory)
    {
        _context = context;
        _gradingFactory = gradingFactory;
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartSession([FromBody] StartSessionRequestDto request)
    {
        var exam = await _context.Exams
            .Include(e => e.Sections)
            .ThenInclude(s => s.Questions)
            .FirstOrDefaultAsync(e => e.Id == request.ExamId);

        if (exam == null) return NotFound(new { message = "Đề thi không tồn tại!" });

        var session = new ExamSession
        {
            UserId = request.UserId,
            ExamId = request.ExamId,
            StartTime = DateTime.UtcNow,
            Status = SessionStatus.InProgress
        };

        await _context.ExamSessions.AddAsync(session);
        await _context.SaveChangesAsync();

        var examDto = new ExamDto
        {
            Id = exam.Id,
            Title = exam.Title,
            Description = exam.Description,
            ExamType = exam.ExamType,
            TotalDurationMinutes = exam.TotalDurationMinutes,
            Sections = exam.Sections.OrderBy(s => s.SectionOrder).Select(s => new ExamSectionDto
            {
                Id = s.Id,
                SkillType = s.SkillType,
                SectionOrder = s.SectionOrder,
                DurationMinutes = s.DurationMinutes,
                MediaUrl = s.MediaUrl,
                PassageText = s.PassageText,
                Questions = s.Questions.OrderBy(q => q.QuestionNumber).Select(q => new QuestionDto
                {
                    Id = q.Id,
                    QuestionNumber = q.QuestionNumber,
                    QuestionType = q.QuestionType,
                    QuestionText = q.QuestionText,
                    OptionsJson = q.OptionsJson,
                    MaxScore = q.MaxScore
                }).ToList()
            }).ToList()
        };

        return Ok(new
        {
            SessionId = session.Id,
            StartTime = session.StartTime,
            Exam = examDto
        });
    }

    [HttpPost("autosave")]
    public async Task<IActionResult> AutosaveDraft([FromBody] AutosaveDraftRequestDto request)
    {
        var session = await _context.ExamSessions.FindAsync(request.SessionId);
        if (session == null) return NotFound(new { message = "Phiên thi không tồn tại!" });

        // Update or create draft in DB
        return Ok(new { message = "Lưu nháp thành công", timestamp = DateTime.UtcNow });
    }

    [HttpPost("submit-skill")]
    public async Task<IActionResult> SubmitSkill([FromBody] SubmitSkillRequestDto request)
    {
        var session = await _context.ExamSessions
            .Include(s => s.Exam)
            .ThenInclude(e => e!.Sections)
            .ThenInclude(sec => sec.Questions)
            .Include(s => s.Submissions)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId);

        if (session == null) return NotFound(new { message = "Phiên thi không tồn tại!" });

        var section = session.Exam?.Sections.FirstOrDefault(s => s.SkillType == request.SkillType);
        if (section == null) return BadRequest(new { message = "Phần thi không tồn tại trong đề thi!" });

        string answersJson = request.Answers != null 
            ? JsonSerializer.Serialize(request.Answers.ToDictionary(k => k.Key.ToString(), v => v.Value))
            : string.Empty;

        var submission = new CandidateSubmission
        {
            SessionId = session.Id,
            SkillType = request.SkillType,
            SubmittedAt = DateTime.UtcNow,
            EssayContent = request.EssayContent,
            AudioRecordUrl = request.AudioRecordUrl,
            AnswersJson = answersJson
        };

        // Use Strategy Pattern to grade
        var grader = _gradingFactory.GetGrader(request.SkillType);
        var gradeResult = await grader.GradeAsync(submission, section);

        submission.RawScore = gradeResult.RawScore;
        submission.BandScore = gradeResult.BandScore;

        var report = new EvaluationReport
        {
            Submission = submission,
            EvaluatedBy = EvaluatorType.AiEngine,
            CriterionScore1 = gradeResult.CriterionScore1,
            CriterionScore2 = gradeResult.CriterionScore2,
            CriterionScore3 = gradeResult.CriterionScore3,
            CriterionScore4 = gradeResult.CriterionScore4,
            FeedbackComments = gradeResult.FeedbackComments,
            DetailedJsonAnalysis = gradeResult.DetailedJsonAnalysis,
            IsApproved = (request.SkillType == SkillType.Listening || request.SkillType == SkillType.Reading) // Auto-approved for objective tests
        };

        submission.EvaluationReports.Add(report);
        session.Submissions.Add(submission);

        // Check if all 4 skills completed
        var submittedSkills = session.Submissions.Select(s => s.SkillType).Distinct().ToList();
        if (submittedSkills.Count >= 4)
        {
            var lSub = session.Submissions.FirstOrDefault(s => s.SkillType == SkillType.Listening);
            var rSub = session.Submissions.FirstOrDefault(s => s.SkillType == SkillType.Reading);
            var wSub = session.Submissions.FirstOrDefault(s => s.SkillType == SkillType.Writing);
            var sSub = session.Submissions.FirstOrDefault(s => s.SkillType == SkillType.Speaking);

            double l = lSub?.BandScore ?? 0;
            double r = rSub?.BandScore ?? 0;
            double w = wSub?.BandScore ?? 0;
            double s = sSub?.BandScore ?? 0;

            session.OverallBandScore = BandScoreCalculator.CalculateOverallBand(l, r, w, s);
            session.Status = SessionStatus.Evaluated;
            session.EndTime = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            SkillType = request.SkillType,
            BandScore = submission.BandScore,
            RawScore = submission.RawScore,
            Report = new
            {
                report.CriterionScore1,
                report.CriterionScore2,
                report.CriterionScore3,
                report.CriterionScore4,
                report.FeedbackComments,
                report.DetailedJsonAnalysis
            }
        });
    }

    [HttpGet("{id}/result")]
    public async Task<IActionResult> GetSessionResult(Guid id)
    {
        var session = await _context.ExamSessions
            .Include(s => s.User)
            .Include(s => s.Exam)
            .Include(s => s.Submissions)
            .ThenInclude(sub => sub.EvaluationReports)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session == null) return NotFound(new { message = "Không tìm thấy kết quả ca thi!" });

        var lSub = session.Submissions.FirstOrDefault(s => s.SkillType == SkillType.Listening);
        var rSub = session.Submissions.FirstOrDefault(s => s.SkillType == SkillType.Reading);
        var wSub = session.Submissions.FirstOrDefault(s => s.SkillType == SkillType.Writing);
        var sSub = session.Submissions.FirstOrDefault(s => s.SkillType == SkillType.Speaking);

        var result = new SessionResultDto
        {
            SessionId = session.Id,
            CandidateName = session.User?.FullName ?? "Thí sinh",
            ExamTitle = session.Exam?.Title ?? "Kỳ thi IELTS",
            Status = session.Status,
            OverallBandScore = session.OverallBandScore,
            ListeningBand = lSub?.BandScore,
            ReadingBand = rSub?.BandScore,
            WritingBand = wSub?.BandScore,
            SpeakingBand = sSub?.BandScore,
            Submissions = session.Submissions.Select(s => new SkillSubmissionDto
            {
                SkillType = s.SkillType,
                RawScore = s.RawScore,
                BandScore = s.BandScore,
                EssayContent = s.EssayContent,
                Reports = s.EvaluationReports.Select(r => new EvaluationReportDto
                {
                    Id = r.Id,
                    EvaluatedBy = r.EvaluatedBy,
                    CriterionScore1 = r.CriterionScore1,
                    CriterionScore2 = r.CriterionScore2,
                    CriterionScore3 = r.CriterionScore3,
                    CriterionScore4 = r.CriterionScore4,
                    FeedbackComments = r.FeedbackComments,
                    DetailedJsonAnalysis = r.DetailedJsonAnalysis,
                    IsApproved = r.IsApproved
                }).ToList()
            }).ToList()
        };

        return Ok(result);
    }
}
