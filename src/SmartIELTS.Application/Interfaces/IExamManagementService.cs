using SmartIELTS.Application.DTOs;
using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Application.Interfaces;

/// <summary>
/// Use case quản trị đề thi (Admin): CRUD Exam -> Section -> Question.
/// <para>
/// Quy tắc nghiệp vụ quan trọng: <b>không cho sửa/xóa</b> đề đã có ca thi
/// để bảo toàn tính toàn vẹn của dữ liệu điểm đã chấm.
/// </para>
/// </summary>
public interface IExamManagementService
{
    Task<IReadOnlyList<ExamAdminDto>> GetAllExamsAsync(CancellationToken ct = default);

    Task<ExamAdminDto?> GetExamAsync(Guid examId, CancellationToken ct = default);

    Task<ExamAdminDto> CreateExamAsync(CreateExamRequestDto request, CancellationToken ct = default);

    Task<ExamAdminDto?> UpdateExamAsync(Guid examId, UpdateExamRequestDto request, CancellationToken ct = default);

    /// <summary>Trả về false nếu đề đã có ca thi (không được xóa).</summary>
    Task<bool> DeleteExamAsync(Guid examId, CancellationToken ct = default);

    /// <summary>Kiểm tra đề đã có ca thi chưa (để trả thông báo rõ ràng cho Admin).</summary>
    Task<bool> ExamHasSessionsAsync(Guid examId, CancellationToken ct = default);

    Task<ExamSectionAdminDto?> AddSectionAsync(Guid examId, CreateSectionRequestDto request, CancellationToken ct = default);

    Task<ExamSectionAdminDto?> UpdateSectionAsync(Guid sectionId, UpdateSectionRequestDto request, CancellationToken ct = default);

    Task<bool> DeleteSectionAsync(Guid sectionId, CancellationToken ct = default);

    Task<QuestionAdminDto?> AddQuestionAsync(Guid sectionId, CreateQuestionRequestDto request, CancellationToken ct = default);

    Task<QuestionAdminDto?> UpdateQuestionAsync(Guid questionId, UpdateQuestionRequestDto request, CancellationToken ct = default);

    Task<bool> DeleteQuestionAsync(Guid questionId, CancellationToken ct = default);
}
