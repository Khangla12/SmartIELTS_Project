using SmartIELTS.Application.DTOs;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;
using SmartIELTS.Infrastructure.Data;
using SmartIELTS.WebApi.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace SmartIELTS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GradingController : ControllerBase
{
    private readonly SmartIeltsDbContext _context;

    public GradingController(SmartIeltsDbContext context)
    {
        _context = context;
    }

    [HttpGet("pending-reviews")]
    public async Task<IActionResult> GetPendingReviews()
    {
        var pendingReports = await _context.EvaluationReports
            .Include(r => r.Submission)
            .ThenInclude(s => s!.Session)
            .ThenInclude(sess => sess!.User)
            .Where(r => !r.IsApproved)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                ReportId = r.Id,
                SubmissionId = r.SubmissionId,
                CandidateName = r.Submission!.Session!.User!.FullName,
                SkillType = r.Submission.SkillType,
                ProposedBandScore = r.Submission.BandScore,
                Criterion1 = r.CriterionScore1,
                Criterion2 = r.CriterionScore2,
                Criterion3 = r.CriterionScore3,
                Criterion4 = r.CriterionScore4,
                AiFeedback = r.FeedbackComments,
                DetailedJsonAnalysis = r.DetailedJsonAnalysis,
                EssayContent = r.Submission.EssayContent,
                SubmittedAt = r.Submission.SubmittedAt
            })
            .ToListAsync();

        return Ok(pendingReports);
    }

    [HttpPost("approve-score")]
    public async Task<IActionResult> ApproveScore([FromBody] TeacherApproveScoreRequestDto request)
    {
        var report = await _context.EvaluationReports
            .Include(r => r.Submission)
            .ThenInclude(s => s!.Session)
            .FirstOrDefaultAsync(r => r.Id == request.ReportId);

        if (report == null) return NotFound(new { message = "Không tìm thấy bản đánh giá!" });

        // Update with Teacher approved score (Human-in-the-loop)
        report.CriterionScore1 = request.CriterionScore1;
        report.CriterionScore2 = request.CriterionScore2;
        report.CriterionScore3 = request.CriterionScore3;
        report.CriterionScore4 = request.CriterionScore4;
        report.FeedbackComments = $"[Giáo viên thẩm định]: {request.TeacherComments}\n[Đánh giá gốc của AI]: {report.FeedbackComments}";
        report.IsApproved = true;

        if (report.Submission != null)
        {
            report.Submission.BandScore = request.ApprovedBandScore;
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = "Giáo viên đã duyệt điểm thành công!", approvedBand = request.ApprovedBandScore });
    }
}

[ApiController]
[Route("api/[controller]")]
public class ProctoringController : ControllerBase
{
    private readonly SmartIeltsDbContext _context;
    private readonly IHubContext<ProctoringHub> _hubContext;

    public ProctoringController(SmartIeltsDbContext context, IHubContext<ProctoringHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    [HttpPost("log")]
    public async Task<IActionResult> LogViolation([FromBody] ProctoringViolationDto request)
    {
        var session = await _context.ExamSessions.FindAsync(request.SessionId);
        if (session == null) return NotFound(new { message = "Phiên thi không tồn tại!" });

        var log = new ProctoringLog
        {
            SessionId = request.SessionId,
            Timestamp = DateTime.UtcNow,
            ViolationType = request.ViolationType,
            Description = request.Description,
            EvidenceSnapshotUrl = request.EvidenceSnapshotUrl
        };

        await _context.ProctoringLogs.AddAsync(log);
        await _context.SaveChangesAsync();

        // Broadcast to Proctoring Realtime Hub
        await _hubContext.Clients.Group($"Proctoring_{request.SessionId}").SendAsync("ViolationAlertReceived", new
        {
            SessionId = request.SessionId,
            ViolationType = request.ViolationType.ToString(),
            Description = request.Description,
            Timestamp = log.Timestamp
        });

        return Ok(new { message = "Đã ghi nhận vi phạm Kiosk", logId = log.Id });
    }

    [HttpGet("logs/{sessionId}")]
    public async Task<IActionResult> GetSessionLogs(Guid sessionId)
    {
        var logs = await _context.ProctoringLogs
            .Where(l => l.SessionId == sessionId)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();

        return Ok(logs);
    }
}
