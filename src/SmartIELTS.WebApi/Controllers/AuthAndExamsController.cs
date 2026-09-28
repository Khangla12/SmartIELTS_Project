using SmartIELTS.Application.DTOs;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;
using SmartIELTS.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace SmartIELTS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IRepository<User> _userRepo;
    private readonly IUnitOfWork _unitOfWork;

    public AuthController(IRepository<User> userRepo, IUnitOfWork unitOfWork)
    {
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var users = await _userRepo.FindAsync(u => u.Username.ToLower() == request.Username.Trim().ToLower());
        var user = users.FirstOrDefault();

        if (user == null || user.PasswordHash != request.Password)
        {
            return Unauthorized(new { message = "Sai tài khoản hoặc mật khẩu!" });
        }

        var response = new LoginResponseDto
        {
            Token = $"mock-jwt-token-{user.Id}-{DateTime.UtcNow.Ticks}",
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role
            }
        };

        return Ok(response);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] UserDto request)
    {
        var existing = await _userRepo.FindAsync(u => u.Username.ToLower() == request.Username.Trim().ToLower());
        if (existing.Any())
        {
            return BadRequest(new { message = "Tên đăng nhập đã tồn tại!" });
        }

        var newUser = new User
        {
            Username = request.Username.Trim(),
            PasswordHash = "Student@123",
            FullName = request.FullName,
            Email = request.Email,
            Role = UserRole.Candidate
        };

        await _userRepo.AddAsync(newUser);
        await _unitOfWork.SaveChangesAsync();

        return Ok(new { message = "Đăng ký thành công!", userId = newUser.Id });
    }
}

[ApiController]
[Route("api/[controller]")]
public class ExamsController : ControllerBase
{
    private readonly IRepository<Exam> _examRepo;

    public ExamsController(IRepository<Exam> examRepo)
    {
        _examRepo = examRepo;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllExams()
    {
        var exams = await _examRepo.GetAllAsync();
        var dtos = exams.Where(e => e.IsActive).Select(e => new ExamDto
        {
            Id = e.Id,
            Title = e.Title,
            Description = e.Description,
            ExamType = e.ExamType,
            TotalDurationMinutes = e.TotalDurationMinutes
        });

        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetExamById(Guid id)
    {
        var exam = await _examRepo.GetByIdAsync(id);
        if (exam == null) return NotFound(new { message = "Không tìm thấy đề thi!" });

        return Ok(exam);
    }
}
