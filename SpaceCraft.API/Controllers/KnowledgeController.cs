using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpaceCraft.API.Data;
using SpaceCraft.API.Models;
using System.Security.Claims;

namespace SpaceCraft.API.Controllers;
[ApiController]
[Route("api/[controller]")]
public class KnowledgeController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<KnowledgeController> _logger;

    public KnowledgeController(ApplicationDbContext context, ILogger<KnowledgeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<KnowledgeItem>>> GetItems(int? parentId, int? userId)
    {
        try
        {
            _logger.LogInformation($"Getting items for parent {parentId} and userId {userId}");
            if (!userId.HasValue || userId.Value <= 0)
            {
                _logger.LogInformation("No valid userId provided, returning empty list");
                return Ok(new List<KnowledgeItem>());
            }

            if (parentId.HasValue)
            {
                var items = await _context.KnowledgeItems
                    .Where(i => i.ParentId == parentId && i.UserId == userId.Value)
                    .ToListAsync();
                _logger.LogInformation($"Found {items.Count} items for parent {parentId}");
                return Ok(items);
            }
            
            var rootItems = await _context.KnowledgeItems
                .Where(i => i.ParentId == null && i.UserId == userId.Value)
                .ToListAsync();
            _logger.LogInformation($"Found {rootItems.Count} root items");
            return Ok(rootItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting items for parent {parentId} and userId {userId}");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<KnowledgeItem>> GetItem(int id)
    {
        try
        {
            _logger.LogInformation($"Getting item {id}");
            var item = await _context.KnowledgeItems.FindAsync(id);
            if (item == null)
            {
                _logger.LogWarning($"Item {id} not found");
                return NotFound();
            }
            _logger.LogInformation($"Found item {id}");
            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting item {id}");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<KnowledgeItem>> CreateItem(KnowledgeItem item)
    {
        try
        {
            _logger.LogInformation($"Creating new item for userId {item.UserId}");
            _logger.LogInformation($"Item data: Title={item.Title}, IsFolder={item.IsFolder}, ParentId={item.ParentId}");
            
            if (item.UserId <= 0)
            {
                _logger.LogWarning("Invalid userId provided");
                return BadRequest("Invalid userId");
            }

            var user = await _context.Users.FindAsync(item.UserId);
            if (user == null)
            {
                _logger.LogWarning($"User with id {item.UserId} not found");
                return BadRequest("User not found");
            }

            item.User = null;
            _context.KnowledgeItems.Add(item);
            await _context.SaveChangesAsync();

            await _context.Entry(item).Reference(i => i.User).LoadAsync();
            
            _logger.LogInformation($"Successfully created item {item.Id}");
            return CreatedAtAction(nameof(GetItem), new { id = item.Id }, item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating item for userId {item.UserId}");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateItem(int id, KnowledgeItem item)
    {
        try
        {
            _logger.LogInformation($"Updating item {id}");
            if (id != item.Id)
            {
                _logger.LogWarning($"Id mismatch: {id} != {item.Id}");
                return BadRequest();
            }

            _context.Entry(item).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Successfully updated item {id}");
            return NoContent();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!ItemExists(id))
            {
                _logger.LogWarning($"Item {id} not found during update");
                return NotFound();
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating item {id}");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteKnowledge(int id)
    {
        try
        {
            _logger.LogInformation($"Delete request received for knowledge item {id}");
            
            foreach (var claim in User.Claims)
            {
                _logger.LogInformation($"Claim: {claim.Type} = {claim.Value}");
            }

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            _logger.LogInformation($"Parsed userId from token: {userId}");

            if (userId <= 0)
            {
                _logger.LogWarning($"Unauthorized delete attempt. UserId: {userId}");
                return Unauthorized();
            }

            var item = await _context.KnowledgeItems.FindAsync(id);
            _logger.LogInformation($"Found item: {item != null}, Item userId: {item?.UserId}, Request userId: {userId}");

            if (item == null)
            {
                _logger.LogWarning($"Item {id} not found");
                return NotFound();
            }

            if (item.UserId != userId)
            {
                _logger.LogWarning($"User {userId} attempted to delete item {id} owned by user {item.UserId}");
                return Unauthorized();
            }

            item.WasPinned = item.IsPinned;
            item.IsPinned = false;

            item.IsDeleted = true;
            item.DeletedAt = DateTime.UtcNow;
            
            if (item.IsFolder)
            {
                await UnpinAndDeleteChildren(item.Id, userId);
            }
            
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Successfully marked item {id} as deleted");
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting knowledge item {id}");
            return StatusCode(500, "An error occurred while deleting the item");
        }
    }

    private async Task UnpinAndDeleteChildren(int parentId, int userId)
    {
        var children = await _context.KnowledgeItems
            .Where(k => k.ParentId == parentId && k.UserId == userId && !k.IsDeleted)
            .ToListAsync();
            
        foreach (var child in children)
        {
            child.WasPinned = child.IsPinned;
            child.IsPinned = false;
            
            child.IsDeleted = true;
            child.DeletedAt = DateTime.UtcNow;
            
            if (child.IsFolder)
            {
                await UnpinAndDeleteChildren(child.Id, userId);
            }
        }
    }

    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<KnowledgeItem>>> GetAllItems(int? userId)
    {
        try
        {
            _logger.LogInformation($"Getting all items for userId {userId}");
            if (!userId.HasValue || userId.Value <= 0)
            {
                _logger.LogInformation("No valid userId provided, returning empty list");
                return Ok(new List<KnowledgeItem>());
            }

            var items = await _context.KnowledgeItems
                .Where(i => i.UserId == userId.Value)
                .ToListAsync();
            _logger.LogInformation($"Found {items.Count} items for user {userId}");
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting all items for userId {userId}");
            return StatusCode(500, "Internal server error");
        }
    }

    private bool ItemExists(int id)
    {
        return _context.KnowledgeItems.Any(e => e.Id == id);
    }
} 