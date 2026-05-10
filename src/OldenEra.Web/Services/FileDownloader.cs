using Microsoft.JSInterop;

namespace OldenEra.Web.Services;

/// <summary>
/// Triggers a browser download for a byte payload via JS interop.
/// </summary>
public sealed class FileDownloader
{
    private readonly IJSRuntime _js;

    public FileDownloader(IJSRuntime js)
    {
        _js = js;
    }

    public async Task DownloadAsync(string filename, string mimeType, byte[] data)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        string base64 = Convert.ToBase64String(data);
        await _js.InvokeVoidAsync("oeDownloader.download", filename, mimeType, base64);
    }
}
