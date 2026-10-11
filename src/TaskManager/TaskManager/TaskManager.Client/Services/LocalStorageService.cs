using Microsoft.JSInterop;
using System.Text.Json;

namespace TaskManager.Client.Services
{
    public class LocalStorageService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly Dictionary<string, Task<string?>> _reads = new();

        public LocalStorageService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public async Task<T?> GetItemAsync<T>(string key)
        {
            if (!_reads.TryGetValue(key, out var read))
            {
                read = _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key).AsTask();
                _reads[key] = read;
            }
            string? json;
            try { json = await read; }
            finally { if (_reads.GetValueOrDefault(key) == read) _reads.Remove(key); }
            return json == null ? default : JsonSerializer.Deserialize<T>(json);
        }

        public async Task SetItemAsync<T>(string key, T value)
        {
            var json = JsonSerializer.Serialize(value);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, json);
            _reads.Remove(key);
        }

        public async Task RemoveItemAsync(string key)
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
            _reads.Remove(key);
        }

        public async Task ClearAsync()
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.clear");
            _reads.Clear();
        }
    }
}
