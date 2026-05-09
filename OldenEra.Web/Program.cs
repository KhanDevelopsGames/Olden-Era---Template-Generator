using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using OldenEra.Web;
using OldenEra.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<FileDownloader>();
builder.Services.AddScoped<BrowserSettingsStore>();
// UpdateChecker hits a cross-origin URL (api.github.com), so it needs an
// HttpClient without a BaseAddress override — keep it isolated from the app
// HttpClient that's pinned to the host base address.
builder.Services.AddScoped<UpdateChecker>(_ => new UpdateChecker(new HttpClient()));

await builder.Build().RunAsync();
