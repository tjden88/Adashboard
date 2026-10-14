using Adashboard.Client.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

// Точка входа WASM-клиента: монтирует корневые компоненты и запускает приложение.
// Регистрация доступа к API (Refit, DashboardApiOptions) и прикладных сервисов
// добавляется по мере реализации соответствующих частей (см. скилл frontend-rewrite).
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Routes>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

await builder.Build().RunAsync();
