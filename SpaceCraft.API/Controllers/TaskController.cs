using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaceCraft.API.Data;
using SpaceCraft.API.Models;
using System.Security.Claims;

namespace SpaceCraft.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TaskController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TaskController> _logger;

    public TaskController(ApplicationDbContext context, ILogger<TaskController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskItem>>> GetTasks(int? userId, TaskCategory? category = null)
    {
        try
        {
            _logger.LogInformation($"Getting tasks for userId {userId} and category {category}");
            if (!userId.HasValue || userId.Value <= 0)
            {
                _logger.LogInformation("No valid userId provided, returning empty list");
                return Ok(new List<TaskItem>());
            }

            var query = _context.TaskItems.Where(t => t.UserId == userId.Value && !t.IsDeleted);
            if (category.HasValue)
            {
                query = query.Where(t => t.Category == category.Value);
            }

            var tasks = await query.ToListAsync();
            _logger.LogInformation($"Found {tasks.Count} tasks");
            return Ok(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting tasks for userId {userId} and category {category}");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("category/{category}")]
    public async Task<ActionResult<IEnumerable<TaskItem>>> GetTasksByCategory(TaskCategory category)
    {
        try
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId <= 0)
            {
                return Unauthorized();
            }

            var tasks = await _context.TaskItems
                .Where(t => t.UserId == userId && t.Category == category && !t.IsDeleted)
                .ToListAsync();
            return Ok(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting tasks for category {category}");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("pinned")]
    public async Task<ActionResult<IEnumerable<TaskItem>>> GetPinnedTasks()
    {
        try
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId <= 0)
            {
                return Unauthorized();
            }

            var tasks = await _context.TaskItems
                .Where(t => t.UserId == userId && t.IsPinned && !t.IsDeleted)
                .OrderBy(t => t.PinOrder)
                .ToListAsync();
            return Ok(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pinned tasks");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TaskItem>> GetTask(int id)
    {
        try
        {
            _logger.LogInformation($"Getting task {id}");
            var task = await _context.TaskItems.FindAsync(id);
            if (task == null)
            {
                _logger.LogWarning($"Task {id} not found");
                return NotFound();
            }
            
            task.Title = task.Title ?? "";
            task.Description = task.Description ?? "";
            
            _logger.LogInformation($"Found task {id}");
            return Ok(task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting task {id}");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<TaskItem>> CreateTask(TaskItem task)
    {
        try
        {
            _logger.LogInformation($"Creating new task for userId {task.UserId}");
            _logger.LogInformation($"Task data: Title={task.Title}, Category={task.Category}");
            
            if (task.UserId <= 0)
            {
                _logger.LogWarning("Invalid userId provided");
                return BadRequest("Invalid userId");
            }

            var user = await _context.Users.FindAsync(task.UserId);
            if (user == null)
            {
                _logger.LogWarning($"User with id {task.UserId} not found");
                return BadRequest("User not found");
            }

            task.Title = task.Title ?? "";
            task.Description = task.Description ?? "";
            
            task.User = user;
            _context.TaskItems.Add(task);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation($"Successfully created task {task.Id}");
            return CreatedAtAction(nameof(GetTask), new { id = task.Id }, task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating task for userId {task.UserId}");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTask(int id, TaskItem task)
    {
        try
        {
            _logger.LogInformation($"Updating task {id}");
            if (id != task.Id)
            {
                _logger.LogWarning($"Id mismatch: {id} != {task.Id}");
                return BadRequest();
            }

            _context.Entry(task).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Successfully updated task {id}");
            return NoContent();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!TaskExists(id))
            {
                _logger.LogWarning($"Task {id} not found during update");
                return NotFound();
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating task {id}");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id}/update")]
    public async Task<IActionResult> UpdateTaskDetails(int id, [FromQuery] string title, [FromQuery] string description)
    {
        try
        {
            _logger.LogInformation($"Updating task {id} with title: {title}, description: {description}");
            
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId <= 0)
            {
                _logger.LogWarning($"Unauthorized update attempt for task {id}");
                return Unauthorized();
            }

            var task = await _context.TaskItems
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId && !t.IsDeleted);

            if (task == null)
            {
                _logger.LogWarning($"Task {id} not found for user {userId}");
                return NotFound();
            }

            task.Title = title ?? "";
            task.Description = description ?? "";
            
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Successfully updated task {id}");
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Database error while updating task {id}");
                return StatusCode(500, "Database error while updating task");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating task {id} details");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id}/category")]
    public async Task<IActionResult> UpdateTaskCategory(int id, [FromQuery] TaskCategory category)
    {
        try
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId <= 0)
            {
                return Unauthorized();
            }

            var task = await _context.TaskItems
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId && !t.IsDeleted);

            if (task == null)
            {
                return NotFound();
            }

            task.Category = category;
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating task {id} category");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("{id}/toggle-pin")]
    public async Task<IActionResult> TogglePin(int id)
    {
        try
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId <= 0)
            {
                return Unauthorized();
            }

            var task = await _context.TaskItems
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId && !t.IsDeleted);

            if (task == null)
            {
                return NotFound();
            }

            task.IsPinned = !task.IsPinned;
            if (task.IsPinned)
            {
                task.WasPinned = true;
                task.PinOrder = await _context.TaskItems
                    .Where(t => t.UserId == userId && t.IsPinned)
                    .CountAsync();
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error toggling pin for task {id}");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("{id}/restore")]
    public async Task<IActionResult> RestoreTask(int id)
    {
        try
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId <= 0)
            {
                return Unauthorized();
            }

            var task = await _context.TaskItems
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId && t.IsDeleted);

            if (task == null)
            {
                return NotFound();
            }

            task.IsDeleted = false;
            task.DeletedAt = null;
            await _context.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error restoring task {id}");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        try
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            if (userId <= 0)
            {
                _logger.LogWarning("Unauthorized delete attempt");
                return Unauthorized();
            }

            var item = await _context.TaskItems
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (item == null)
            {
                _logger.LogWarning($"Task {id} not found for user {userId}");
                return NotFound();
            }

            item.IsDeleted = true;
            item.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Marked task {id} as deleted for user {userId}");
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error marking task {id} as deleted");
            return StatusCode(500, "Internal server error");
        }
    }

    private bool TaskExists(int id)
    {
        return _context.TaskItems.Any(e => e.Id == id);
    }
} 