using System.Text.Json;
using Microsoft.JSInterop;
using OldenEra.Generator.Models;

namespace OldenEra.Web.Services;

/// <summary>
/// Persists the user's <see cref="SettingsFile"/> to browser localStorage so
/// settings survive a page reload — the WASM equivalent of the WPF app
/// remembering settings between sessions.
///
/// All interop is wrapped in try/catch because some browsers (Safari private
/// mode, locked-down enterprise profiles) throw on localStorage access. We
/// degrade silently rather than blocking the UI.
/// </summary>
public sealed class BrowserSettingsStore
{
    private const string Key = "oldenEra.settings";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IJSRuntime _js;

    public BrowserSettingsStore(IJSRuntime js) => _js = js;

    public async Task<SettingsFile?> LoadAsync()
    {
        try
        {
            var raw = await _js.InvokeAsync<string?>("localStorage.getItem", Key);
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return JsonSerializer.Deserialize<SettingsFile>(raw, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveAsync(SettingsFile settings)
    {
        try
        {
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            await _js.InvokeVoidAsync("localStorage.setItem", Key, json);
        }
        catch
        {
            // Silently ignore — storage may be disabled or full.
        }
    }

    public async Task ClearAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", Key);
        }
        catch
        {
            // Silently ignore.
        }
    }
}
