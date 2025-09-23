using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using Microsoft.JSInterop;
using System.Diagnostics;

public class TaskService
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private int _currentUserId;
    private bool _isInitialized;

    public TaskService(HttpClient httpClient, IJSRuntime jsRuntime)
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
            var token = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "token");
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                Debug.WriteLine("Added auth token to request headers");
            }
            else
            {
                Debug.WriteLine("No auth token found in localStorage");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error adding auth header: {ex}");
        }
    }

    public async Task<TaskItem> Add(TaskItem item)
    {
        try
        {
            await EnsureInitialized();
            await AddAuthHeader();
            Debug.WriteLine($"Adding new task with userId: {_currentUserId}");
            Debug.WriteLine($"Task data: Title={item.Title}, Category={item.Category}");
            
            item.Title = item.Title ?? "";
            item.Description = item.Description ?? "";
            
            item.UserId = _currentUserId;
            var response = await _httpClient.PostAsJsonAsync("api/task", item);
            Debug.WriteLine($"Response status: {response.StatusCode}");
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"Error response: {errorContent}");
                throw new Exception($"Failed to create task: {errorContent}");
            }
            var result = await response.Content.ReadFromJsonAsync<TaskItem>();
            Debug.WriteLine($"Successfully added task with id: {result?.Id}");
            return result!;
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
            var response = await _httpClient.DeleteAsync($"api/task/{id}");
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to delete task: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in Delete: {ex}");
            throw;
        }
    }

    public async Task RestoreTask(int id)
    {
        await EnsureInitialized();
        await AddAuthHeader();
        var response = await _httpClient.PostAsync($"api/task/{id}/restore", null);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to restore task: {errorContent}");
        }
    }

    public async Task UpdateCategory(int id, TaskCategory category)
    {
        try
        {
            await EnsureInitialized();
            await AddAuthHeader();
            var response = await _httpClient.PutAsync($"api/task/{id}/category?category={category}", null);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to update task category: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in UpdateCategory: {ex}");
            throw;
        }
    }

    public async Task UpdateTask(int id, string title, string description)
    {
        try
        {
            await EnsureInitialized();
            await AddAuthHeader();
            
            var existingTask = await Get(id);
            if (existingTask == null)
            {
                throw new Exception($"Task with id {id} not found");
            }
            
            existingTask.Title = title ?? "";
            existingTask.Description = description ?? "";
            
            var response = await _httpClient.PutAsJsonAsync($"api/task/{id}", existingTask);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"Error response: {errorContent}");
                throw new Exception($"Failed to update task: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in UpdateTask: {ex}");
            throw;
        }
    }

    public async Task<TaskItem?> Get(int id)
    {
        try
        {
            await EnsureInitialized();
            await AddAuthHeader();
            var response = await _httpClient.GetAsync($"api/task/{id}");
            if (response.IsSuccessStatusCode)
            {
                var task = await response.Content.ReadFromJsonAsync<TaskItem>();
                if (task != null)
                {
                    task.Title = task.Title ?? "";
                    task.Description = task.Description ?? "";
                }
                return task;
            }
            Debug.WriteLine($"Failed to get task {id}: {response.StatusCode}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in Get: {ex}");
            return null;
        }
    }

    public async Task<List<TaskItem>> GetByCategory(TaskCategory? category)
    {
        try
        {
            await EnsureInitialized();
            await AddAuthHeader();
            var url = category.HasValue
                ? $"api/task/category/{category}"
                : "api/task";
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var tasks = await response.Content.ReadFromJsonAsync<List<TaskItem>>();
                return tasks ?? new List<TaskItem>();
            }
            return new List<TaskItem>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in GetByCategory: {ex}");
            return new List<TaskItem>();
        }
    }

    public async Task<List<TaskItem>> GetPinnedTasks()
    {
        try
        {
            await EnsureInitialized();
            await AddAuthHeader();
            var response = await _httpClient.GetAsync("api/task/pinned");
            if (response.IsSuccessStatusCode)
            {
                var tasks = await response.Content.ReadFromJsonAsync<List<TaskItem>>();
                return tasks ?? new List<TaskItem>();
            }
            return new List<TaskItem>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in GetPinnedTasks: {ex}");
            return new List<TaskItem>();
        }
    }

    public async Task TogglePin(int id)
    {
        try
        {
            await EnsureInitialized();
            await AddAuthHeader();
            var response = await _httpClient.PostAsync($"api/task/{id}/toggle-pin", null);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to toggle pin: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in TogglePin: {ex}");
            throw;
        }
    }

    public void ClearCache()
    {
        _isInitialized = false;
    }

    public async Task Update(int id, TaskItem item)
    {
        try
        {
            await EnsureInitialized();
            await AddAuthHeader();
            var response = await _httpClient.PutAsJsonAsync($"api/task/{id}", item);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to update task: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in Update: {ex}");
            throw;
        }
    }

    public async Task<TaskItem> CreateTask(string title, TaskCategory category, string description = "")
    {
        try
        {
            await EnsureInitialized();
            await AddAuthHeader();
            
            title = title ?? "";
            description = description ?? "";
            
            var task = new TaskItem
            {
                Title = title,
                Description = description,
                Category = category,
                CreatedAt = DateTime.Now,
                IsPinned = false,
                WasPinned = false,
                PinOrder = 0,
                IsDeleted = false,
                DeletedAt = null
            };
            
            Debug.WriteLine($"Creating new task: Title={title}, Category={category}");
            var result = await Add(task);
            Debug.WriteLine($"Successfully created task with id: {result.Id}");
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in CreateTask: {ex}");
            throw;
        }
    }
}