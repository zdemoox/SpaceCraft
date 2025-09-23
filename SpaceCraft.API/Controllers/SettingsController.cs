using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaceCraft.API.Data;
using SpaceCraft.API.Models;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SpaceCraft.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(ApplicationDbContext context, ILogger<SettingsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null) return null;
        if (!int.TryParse(userIdClaim.Value, out int userId)) return null;
        return userId;
    }

    [HttpPost("change-credentials")]
    public async Task<IActionResult> ChangeCredentials([FromBody] ChangeCredentialsRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.NewUsername))
            return BadRequest("Имя пользователя не может быть пустым");
        
        if (string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest("Пароль не может быть пустым");

        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (user == null)
            return Unauthorized();

        bool usernameChanged = !string.Equals(user.Username, req.NewUsername, StringComparison.OrdinalIgnoreCase);
        bool passwordChanged = !VerifyPassword(req.NewPassword, user.PasswordHash);

        if (!usernameChanged && !passwordChanged)
            return BadRequest("No changes made.");

        if (usernameChanged)
        {
            bool taken = await _context.Users.AnyAsync(u => u.Username == req.NewUsername && u.Id != userId.Value);
            if (taken)
                return Conflict("Username already taken.");
            user.Username = req.NewUsername;
        }
        if (passwordChanged)
            user.PasswordHash = HashPassword(req.NewPassword);

        await _context.SaveChangesAsync();
        return Ok();
    }

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    private bool VerifyPassword(string password, string hash)
    {
        return HashPassword(password) == hash;
    }
}

public class ChangeCredentialsRequest
{
    public required string NewUsername { get; set; }
    public required string NewPassword { get; set; }
} 