using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using SpaceCraft.API.Data;
using SpaceCraft.API.Models;
using Microsoft.Extensions.Logging;

namespace SpaceCraft.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(ApplicationDbContext context, IConfiguration configuration, ILogger<AuthController> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("test")]
    public IActionResult Test()
    {
        Console.WriteLine("Test endpoint called");
        return Ok("API is working!");
    }

    [HttpGet("ping")]
    public IActionResult Ping()
    {
        Console.WriteLine("Ping endpoint called");
        return Ok(new { message = "pong", time = DateTime.UtcNow });
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterDto request)
    {
        Console.WriteLine($"=== REGISTER ATTEMPT ===");
        Console.WriteLine($"Username: {request.Username}");
        Console.WriteLine($"Password length: {request.Password?.Length ?? 0}");

        try
        {
            Console.WriteLine("Checking database connection...");
            var canConnect = await _context.Database.CanConnectAsync();
            Console.WriteLine($"Database connection status: {canConnect}");

            var userCount = await _context.Users.CountAsync();
            Console.WriteLine($"Current user count in database: {userCount}");

            var existingUser = await _context.Users.AnyAsync(u => u.Username == request.Username);
            Console.WriteLine($"User exists check result: {existingUser}");

            if (existingUser)
            {
                Console.WriteLine($"Registration failed: User {request.Username} already exists");
                return BadRequest("Пользователь с таким именем уже существует");
            }

            var user = new User
            {
                Username = request.Username,
                PasswordHash = HashPassword(request.Password),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            Console.WriteLine($"User {request.Username} successfully registered");

            var token = GenerateJwtToken(user);
            return new AuthResponse(token, user);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during registration: {ex}");
            throw;
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginDto request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user == null)
        {
            return BadRequest("Пользователь не найден");
        }

        if (user.PasswordHash != HashPassword(request.Password))
        {
            return BadRequest("Неверный пароль");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var token = GenerateJwtToken(user);
        return new AuthResponse(token, user);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user == null)
        {
            return BadRequest("Пользователь не найден");
        }

        user.PasswordHash = HashPassword(request.NewPassword);
        await _context.SaveChangesAsync();

        return Ok();
    }

    private string GenerateJwtToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.Now.AddDays(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }
}

public record RegisterDto(string Username, string Password);
public record LoginDto(string Username, string Password);
public record ResetPasswordDto(string Username, string NewPassword);
public record AuthResponse(string Token, User User); 