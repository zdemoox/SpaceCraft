using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaceCraft.API.Data;
using System.Security.Claims;

namespace SpaceCraft.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ThemeController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ThemeController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<string>> GetUserTheme()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var user = await _context.Users
                    .Where(u => u.Id == userId.Value)
                    .FirstOrDefaultAsync();
                    
                if (user == null)
                    return NotFound();

                return Ok(user.Theme);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        [HttpPut]
        public async Task<ActionResult> UpdateUserTheme([FromBody] string theme)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                if (theme != "light" && theme != "dark")
                    return BadRequest(new { message = "Theme must be 'light' or 'dark'" });

                var user = await _context.Users
                    .Where(u => u.Id == userId.Value)
                    .FirstOrDefaultAsync();
                    
                if (user == null)
                    return NotFound();

                user.Theme = theme;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Theme updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                return userId;
            return null;
        }
    }
} 