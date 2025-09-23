using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaceCraft.API.Data;
using SpaceCraft.API.Models;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace SpaceCraft.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/navigation-stack")]
    public class NavigationStackController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public NavigationStackController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetStack()
        {
            var userId = HttpContext.User.Identity.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == userId);
            
            if (user == null)
                return Unauthorized();

            var stack = await _context.NavigationStackItems
                .Where(x => x.UserId == user.Id)
                .OrderBy(x => x.Timestamp)
                .ToListAsync();
            return Ok(stack);
        }

        [HttpPost]
        public async Task<IActionResult> Push([FromBody] string url)
        {
            var userId = HttpContext.User.Identity.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == userId);
            
            if (user == null)
                return Unauthorized();

            var item = new NavigationStackItem 
            { 
                UserId = user.Id, 
                Url = url, 
                Timestamp = DateTime.UtcNow 
            };
            _context.NavigationStackItems.Add(item);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> Pop()
        {
            var userId = HttpContext.User.Identity.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == userId);
            
            if (user == null)
                return Unauthorized();

            var last = await _context.NavigationStackItems
                .Where(x => x.UserId == user.Id)
                .OrderByDescending(x => x.Timestamp)
                .FirstOrDefaultAsync();
            
            if (last != null)
            {
                _context.NavigationStackItems.Remove(last);
                await _context.SaveChangesAsync();
                return Ok(last.Url);
            }
            return NotFound();
        }

        [HttpDelete("clear")]
        public async Task<IActionResult> Clear()
        {
            var userId = HttpContext.User.Identity.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == userId);
            
            if (user == null)
                return Unauthorized();

            var items = await _context.NavigationStackItems
                .Where(x => x.UserId == user.Id)
                .ToListAsync();

            _context.NavigationStackItems.RemoveRange(items);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
} 