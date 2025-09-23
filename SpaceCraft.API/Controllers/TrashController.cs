using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaceCraft.API.Data;
using SpaceCraft.API.Models;
using System.Security.Claims;
using System.Diagnostics;

namespace SpaceCraft.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrashController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TrashController> _logger;

        public TrashController(ApplicationDbContext context, ILogger<TrashController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                _logger.LogWarning("No user ID claim found in token");
                return null;
            }

            if (!int.TryParse(userIdClaim.Value, out int userId))
            {
                _logger.LogWarning("Failed to parse user ID from claim: {UserId}", userIdClaim.Value);
                return null;
            }

            _logger.LogInformation("Successfully extracted user ID: {UserId}", userId);
            return userId;
        }

        [HttpGet("knowledge")]
        public async Task<ActionResult<IEnumerable<KnowledgeItem>>> GetTrashedKnowledge()
        {
            _logger.LogInformation("Attempting to get trashed knowledge items");
            
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Unauthorized access attempt to trash - no valid user ID");
                return Unauthorized();
            }

            try
            {
                var allDeletedItems = await _context.KnowledgeItems
                    .Where(k => k.UserId == userId && k.IsDeleted)
                    .ToListAsync();
                    
                var deletedFolderIds = allDeletedItems
                    .Where(k => k.IsFolder)
                    .Select(k => k.Id)
                    .ToList();
                    
                var visibleItems = allDeletedItems
                    .Where(k => k.ParentId == null || !deletedFolderIds.Contains(k.ParentId.Value))
                    .OrderByDescending(k => k.DeletedAt)
                    .ToList();

                _logger.LogInformation("Successfully retrieved {Count} trashed knowledge items for user {UserId}", 
                    visibleItems.Count, userId);
                return Ok(visibleItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trashed knowledge items for user {UserId}", userId);
                return StatusCode(500, "An error occurred while retrieving trashed items");
            }
        }

        [HttpGet("tasks")]
        public async Task<ActionResult<IEnumerable<TaskItem>>> GetTrashedTasks()
        {
            _logger.LogInformation("Attempting to get trashed tasks");
            
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Unauthorized access attempt to trash - no valid user ID");
                return Unauthorized();
            }

            try
            {
                var items = await _context.TaskItems
                    .Where(t => t.UserId == userId && t.IsDeleted)
                    .OrderByDescending(t => t.DeletedAt)
                    .ToListAsync();

                _logger.LogInformation("Successfully retrieved {Count} trashed tasks for user {UserId}", 
                    items.Count, userId);
                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trashed tasks for user {UserId}", userId);
                return StatusCode(500, "An error occurred while retrieving trashed items");
            }
        }

        [HttpPost("restore/knowledge/{id}")]
        public async Task<IActionResult> RestoreKnowledge(int id)
        {
            _logger.LogInformation("Attempting to restore knowledge item {Id}", id);
            
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Unauthorized access attempt to restore knowledge - no valid user ID");
                return Unauthorized();
            }

            try
            {
                var item = await _context.KnowledgeItems
                    .FirstOrDefaultAsync(k => k.Id == id && k.UserId == userId && k.IsDeleted);

                if (item == null)
                {
                    _logger.LogWarning("Knowledge item {Id} not found or not deleted for user {UserId}", id, userId);
                    return NotFound();
                }

                item.IsDeleted = false;
                item.DeletedAt = null;
                
                if (item.WasPinned)
                {
                    item.IsPinned = true;
                }
                
                if (item.IsFolder)
                {
                    await RestoreChildren(item.Id, userId.Value);
                }
                
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully restored knowledge item {Id} for user {UserId}", id, userId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring knowledge item {Id} for user {UserId}", id, userId);
                return StatusCode(500, "An error occurred while restoring the item");
            }
        }
        
        private async Task RestoreChildren(int parentId, int userId)
        {
            var children = await _context.KnowledgeItems
                .Where(k => k.ParentId == parentId && k.UserId == userId && k.IsDeleted)
                .ToListAsync();
                
            foreach (var child in children)
            {
                child.IsDeleted = false;
                child.DeletedAt = null;
                
                if (child.WasPinned)
                {
                    child.IsPinned = true;
                }
                
                if (child.IsFolder)
                {
                    await RestoreChildren(child.Id, userId);
                }
            }
        }

        [HttpPost("restore/task/{id}")]
        public async Task<IActionResult> RestoreTask(int id)
        {
            _logger.LogInformation("Attempting to restore task {Id}", id);
            
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Unauthorized access attempt to restore task - no valid user ID");
                return Unauthorized();
            }

            try
            {
                var item = await _context.TaskItems
                    .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId && t.IsDeleted);

                if (item == null)
                {
                    _logger.LogWarning("Task {Id} not found or not deleted for user {UserId}", id, userId);
                    return NotFound();
                }

                item.IsDeleted = false;
                item.DeletedAt = null;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully restored task {Id} for user {UserId}", id, userId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring task {Id} for user {UserId}", id, userId);
                return StatusCode(500, "An error occurred while restoring the item");
            }
        }

        [HttpDelete("knowledge/{id}")]
        public async Task<IActionResult> PermanentlyDeleteKnowledge(int id)
        {
            _logger.LogInformation("Attempting to permanently delete knowledge item {Id}", id);
            
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Unauthorized access attempt to delete knowledge - no valid user ID");
                return Unauthorized();
            }

            try
            {
                var item = await _context.KnowledgeItems
                    .FirstOrDefaultAsync(k => k.Id == id && k.UserId == userId && k.IsDeleted);

                if (item == null)
                {
                    _logger.LogWarning("Knowledge item {Id} not found or not deleted for user {UserId}", id, userId);
                    return NotFound();
                }

                if (item.IsFolder)
                {
                    await PermanentlyDeleteChildren(item.Id, userId.Value);
                }

                _context.KnowledgeItems.Remove(item);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully permanently deleted knowledge item {Id} for user {UserId}", id, userId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error permanently deleting knowledge item {Id} for user {UserId}", id, userId);
                return StatusCode(500, "An error occurred while deleting the item");
            }
        }
        
        private async Task PermanentlyDeleteChildren(int parentId, int userId)
        {
            var children = await _context.KnowledgeItems
                .Where(k => k.ParentId == parentId && k.UserId == userId && k.IsDeleted)
                .ToListAsync();
                
            foreach (var child in children)
            {
                if (child.IsFolder)
                {
                    await PermanentlyDeleteChildren(child.Id, userId);
                }
                
                _context.KnowledgeItems.Remove(child);
            }
        }

        [HttpDelete("task/{id}")]
        public async Task<IActionResult> PermanentlyDeleteTask(int id)
        {
            _logger.LogInformation("Attempting to permanently delete task {Id}", id);
            
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                _logger.LogWarning("Unauthorized access attempt to delete task - no valid user ID");
                return Unauthorized();
            }

            try
            {
                var item = await _context.TaskItems
                    .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId && t.IsDeleted);

                if (item == null)
                {
                    _logger.LogWarning("Task {Id} not found or not deleted for user {UserId}", id, userId);
                    return NotFound();
                }

                _context.TaskItems.Remove(item);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully permanently deleted task {Id} for user {UserId}", id, userId);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error permanently deleting task {Id} for user {UserId}", id, userId);
                return StatusCode(500, "An error occurred while deleting the item");
            }
        }
    }
} 