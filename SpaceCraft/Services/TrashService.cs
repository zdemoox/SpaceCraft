using System.Collections.Generic;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using SpaceCraft.Models;
using System.Diagnostics;

namespace SpaceCraft.Services
{
    public class TrashService
    {
        private readonly HttpClient _httpClient;
        private readonly IJSRuntime _jsRuntime;
        private int _currentUserId;
        private bool _isInitialized;

        public TrashService(HttpClient httpClient, IJSRuntime jsRuntime)
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

        public async Task<List<KnowledgeItem>> GetTrashedKnowledge()
        {
            try
            {
                await EnsureInitialized();
                if (_currentUserId <= 0)
                {
                    Debug.WriteLine("User not authenticated, returning empty list");
                    return new List<KnowledgeItem>();
                }

                await AddAuthHeader();
                var response = await _httpClient.GetFromJsonAsync<List<KnowledgeItem>>("api/trash/knowledge");
                return response ?? new List<KnowledgeItem>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting trashed knowledge: {ex.Message}");
                return new List<KnowledgeItem>();
            }
        }

        public async Task<List<TaskItem>> GetTrashedTasks()
        {
            try
            {
                await EnsureInitialized();
                if (_currentUserId <= 0)
                {
                    Debug.WriteLine("User not authenticated, returning empty list");
                    return new List<TaskItem>();
                }

                await AddAuthHeader();
                var response = await _httpClient.GetFromJsonAsync<List<TaskItem>>("api/trash/tasks");
                return response ?? new List<TaskItem>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting trashed tasks: {ex.Message}");
                return new List<TaskItem>();
            }
        }

        public async Task<bool> RestoreKnowledge(int id)
        {
            try
            {
                await EnsureInitialized();
                if (_currentUserId <= 0)
                {
                    Debug.WriteLine("User not authenticated");
                    return false;
                }

                await AddAuthHeader();
                var response = await _httpClient.PostAsync($"api/trash/restore/knowledge/{id}", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error restoring knowledge item: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RestoreTask(int id)
        {
            try
            {
                await EnsureInitialized();
                if (_currentUserId <= 0)
                {
                    Debug.WriteLine("User not authenticated");
                    return false;
                }

                await AddAuthHeader();
                var response = await _httpClient.PostAsync($"api/trash/restore/task/{id}", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error restoring task: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> PermanentlyDeleteKnowledge(int id)
        {
            try
            {
                await EnsureInitialized();
                if (_currentUserId <= 0)
                {
                    Debug.WriteLine("User not authenticated");
                    return false;
                }

                await AddAuthHeader();
                var response = await _httpClient.DeleteAsync($"api/trash/knowledge/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error permanently deleting knowledge item: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> PermanentlyDeleteTask(int id)
        {
            try
            {
                await EnsureInitialized();
                if (_currentUserId <= 0)
                {
                    Debug.WriteLine("User not authenticated");
                    return false;
                }

                await AddAuthHeader();
                var response = await _httpClient.DeleteAsync($"api/trash/task/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error permanently deleting task: {ex.Message}");
                return false;
            }
        }
    }
}

public interface ITrashable
{
    Guid Id { get; }
    string Name { get; }
    DateTime DeletedAt { get; set; }
}

public class TrashItem : ITrashable
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public DateTime DeletedAt { get; set; }
    public object OriginalData { get; set; }
    public string ItemType { get; set; }
}