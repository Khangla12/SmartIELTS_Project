using SmartIELTS.Application.DTOs;
using SmartIELTS.Domain.Common;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;
using SmartIELTS.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace SmartIELTS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IRepository<User> _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtOptions _jwtOptions;

    public AuthController(IRepository<User> userRepo, IUnitOfWork unitOfWork, IOptions<JwtOptions> jwtOptions)
    {
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
        _jwtOptions = jwtOptions.Value;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken ct)
    {
        var users = await _userRepo.FindAsync(u => u.Username.ToLower() == request.Username.Trim().ToLower());
        var user = users.FirstOrDefault();

        if (user == null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Sai tài khoản hoặc mật khẩu!" });
        }

        // Tự nâng cấp mật khẩu plaintext của dữ liệu seed cũ sang PBKDF2.
        if (!PasswordHasher.IsHashed(user.PasswordHash))
        {
            user.PasswordHash = PasswordHasher.Hash(request.Password);
            _userRepo.Update(user);
            await _unitOfWork.SaveChangesAsync(ct);
        }

        var response = new LoginResponseDto
        {
            Token = JwtTokenGenerator.Generate(user, _jwtOptions),
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
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken ct)
    {
        var existing = await _userRepo.FindAsync(u => u.Username.ToLower() == request.Username.Trim().ToLower());
        if (existing.Any())
        {
            return BadRequest(new { message = "Tên đăng nhập đã tồn tại!" });
        }

        var newUser = new User
        {
            Username = request.Username.Trim(),
            PasswordHash = PasswordHasher.Hash(request.Password),
            FullName = request.FullName,
            Email = request.Email,
            Role = UserRole.Candidate
        };

        await _userRepo.AddAsync(newUser);
        await _unitOfWork.SaveChangesAsync(ct);

        return Ok(new { message = "Đăng ký thành công!", userId = newUser.Id });
    }
}
