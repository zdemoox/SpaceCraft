using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using Microsoft.JSInterop;
using System.Diagnostics;

public class KnowledgeService
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private int _currentUserId;
    private bool _isInitialized;

    public KnowledgeService(HttpClient httpClient, IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
    }

    private async Task EnsureInitialized()
    {
        if (!_isInitialized)
        {
            try
            {
                var userId = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "userId");
                Debug.WriteLine($"Loading userId from localStorage: {userId}");
                if (int.TryParse(userId, out int id))
                {
                    _currentUserId = id;
                    Debug.WriteLine($"Successfully parsed userId: {_currentUserId}");
                }
                else
                {
                    Debug.WriteLine("Failed to parse userId");
                    _currentUserId = 0;
                }
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in EnsureInitialized: {ex}");
                _currentUserId = 0;
                _isInitialized = true;
            }
        }
        else
        {
            var currentUserId = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "userId");
            if (int.TryParse(currentUserId, out int id) && id != _currentUserId)
            {
                Debug.WriteLine($"UserId changed from {_currentUserId} to {id}");
                _currentUserId = id;
                _isInitialized = false;
                await EnsureInitialized();
            }
        }
    }

    private async Task AddAuthHeader()
    {
        try
        {
            Debug.WriteLine("Attempting to get token from localStorage");
            var token = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "token");
            Debug.WriteLine($"Token from localStorage: {(string.IsNullOrEmpty(token) ? "null or empty" : "exists")}");
            
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                Debug.WriteLine($"Added auth token to request headers: Bearer {token.Substring(0, Math.Min(10, token.Length))}...");
            }
            else
            {
                Debug.WriteLine("No auth token found in localStorage");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error adding auth header: {ex}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    public async Task<KnowledgeItem> Add(KnowledgeItem item)
    {
        try
        {
            await EnsureInitialized();
            await AddAuthHeader();
            Debug.WriteLine($"Adding new item with userId: {_currentUserId}");
            Debug.WriteLine($"Item data: Title={item.Title}, IsFolder={item.IsFolder}, ParentId={item.ParentId}");
            item.UserId = _currentUserId;
            var response = await _httpClient.PostAsJsonAsync("api/knowledge", item);
            Debug.WriteLine($"Response status: {response.StatusCode}");
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"Error response: {errorContent}");
                throw new Exception($"Failed to create item: {errorContent}");
            }
            var result = await response.Content.ReadFromJsonAsync<KnowledgeItem>();
            Debug.WriteLine($"Successfully added item with id: {result?.Id}");
            return result ?? throw new Exception("Failed to create item");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in Add: {ex}");
            throw;
        }
    }

    public async Task Delete(int id)
    {
        try
        {
            await EnsureInitialized();
            await AddAuthHeader();
            Debug.WriteLine($"Deleting item {id} for userId: {_currentUserId}");
            var response = await _httpClient.DeleteAsync($"api/knowledge/{id}");
            response.EnsureSuccessStatusCode();
            Debug.WriteLine($"Successfully deleted item {id}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in Delete: {ex}");
            throw;
        }
    }

    public async Task Restore(int id)
    {
        throw new NotImplementedException();
    }

    public async Task Rename(int id, string newTitle)
    {
        try
        {
            await EnsureInitialized();
            await AddAuthHeader();
            Debug.WriteLine($"Renaming item {id} to {newTitle}");
            var item = await Get(id);
            if (item != null)
            {
                item.Title = newTitle;
                await UpdateItem(id, item);
                Debug.WriteLine($"Successfully renamed item {id}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in Rename: {ex}");
            throw;
        }
    }

    public async Task<List<KnowledgeItem>> GetByParent(int? parentId)
    {
        try
        {
            await EnsureInitialized();
            await AddAuthHeader();
            if (_currentUserId <= 0)
            {
                Debug.WriteLine("User not authenticated, returning empty list");
                return new List<KnowledgeItem>();
            }

            Debug.WriteLine($"Getting items for parent {parentId} and userId: {_currentUserId}");
            var response = await _httpClient.GetFromJsonAsync<List<KnowledgeItem>>($"api/knowledge?parentId={parentId}&userId={_currentUserId}");
            Debug.WriteLine($"Found {response?.Count ?? 0} items before filtering");
            
            var filteredItems = response?.Where(i => !i.IsDeleted).ToList() ?? new List<KnowledgeItem>();
            Debug.WriteLine($"Found {filteredItems.Count} items after filtering out deleted ones");
            
            return filteredItems;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in GetByParent: {ex}");
            throw;
        }
    }

    public async Task<KnowledgeItem?> Get(int id)
    {
        try
        {
            await EnsureInitialized();
            await AddAuthHeader();
            Debug.WriteLine($"Getting item {id} for userId: {_currentUserId}");
            var item = await _httpClient.GetFromJsonAsync<KnowledgeItem>($"api/knowledge/{id}");
            Debug.WriteLine($"Found item: {item != null}");
            return item;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in Get: {ex}");
            return null;
        }
    }

    private async Task<IEnumerable<KnowledgeItem>> GetAllChildren(int parentId)
    {
        try
        {
            await EnsureInitialized();
            Debug.WriteLine($"Getting all children for parent {parentId}");
            var items = await GetByParent(parentId);
            var result = new List<KnowledgeItem>(items);
            
            foreach (var item in items)
            {
                if (item.IsFolder)
                {
                    result.AddRange(await GetAllChildren(item.Id));
                }
            }
            
            Debug.WriteLine($"Found {result.Count} total children");
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in GetAllChildren: {ex}");
            throw;
        }
    }

    public async Task SaveContent(int id, string content)
    {
        try
        {
            await EnsureInitialized();
            await AddAuthHeader();
            Debug.WriteLine($"Saving content for item {id}");
            var item = await Get(id);
            if (item != null)
            {
                item.Content = content;
                await UpdateItem(id, item);
                Debug.WriteLine($"Successfully saved content for item {id}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in SaveContent: {ex}");
            throw;
        }
    }

    public async Task SaveTitle(int id, string title)
    {
        try
        {
            await EnsureInitialized();
            await AddAuthHeader();
            Debug.WriteLine($"Saving title for item {id}");
            var item = await Get(id);
            if (item != null)
            {
                item.Title = title;
                await UpdateItem(id, item);
                Debug.WriteLine($"Successfully saved title for item {id}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in SaveTitle: {ex}");
            throw;
        }
    }

    public async Task TogglePin(int id)
    {
        try
        {
            await EnsureInitialized();
            await AddAuthHeader();
            Debug.WriteLine($"Toggling pin for item {id}");
            var item = await Get(id);
            if (item != null)
            {
                item.IsPinned = !item.IsPinned;
                await UpdateItem(id, item);
                Debug.WriteLine($"Successfully toggled pin for item {id}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in TogglePin: {ex}");
            throw;
        }
    }

    public async Task<List<KnowledgeItem>> GetPinnedItems()
    {
        try
        {
            await EnsureInitialized();
            await AddAuthHeader();
            Debug.WriteLine("Getting pinned items");
            
            var response = await _httpClient.GetFromJsonAsync<List<KnowledgeItem>>($"api/knowledge/all?userId={_currentUserId}");
            
            var pinnedItems = response?
                .Where(i => i.IsPinned && !i.IsDeleted)
                .OrderBy(i => i.PinOrder)
                .ToList() ?? new List<KnowledgeItem>();
                
            Debug.WriteLine($"Found {pinnedItems.Count} pinned items");
            return pinnedItems;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in GetPinnedItems: {ex}");
            throw;
        }
    }

    private async Task UpdateItem(int id, KnowledgeItem item)
    {
        try
        {
            await EnsureInitialized();
            await AddAuthHeader();
            Debug.WriteLine($"Updating item {id}");
            var response = await _httpClient.PutAsJsonAsync($"api/knowledge/{id}", item);
            response.EnsureSuccessStatusCode();
            Debug.WriteLine($"Successfully updated item {id}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in UpdateItem: {ex}");
            throw;
        }
    }

    public void ClearCache()
    {
        _isInitialized = false;
        _currentUserId = 0;
    }
}
