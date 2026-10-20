using AdminToolWeb.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Registrar servicios como Scoped para que cada sesión tenga su instancia
builder.Services.AddScoped<SesionService>();
builder.Services.AddScoped<InventarioService>();
builder.Services.AddScoped<RenombrarService>();
builder.Services.AddScoped<WinRMService>();

// Escuchar en todas las interfaces de la red local
builder.WebHost.UseUrls("http://0.0.0.0:5000");

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<AdminToolWeb.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
