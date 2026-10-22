using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SaborAQuisqueya.Data;

var builder = WebApplication.CreateBuilder(args);

// Agregar servicios MVC
builder.Services.AddControllersWithViews();

// Configurar conexión a SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
options.UseSqlServer(
builder.Configuration.GetConnectionString("DefaultConnection")
)
);

// Configurar autenticación por cookies
builder.Services
.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
.AddCookie(options =>
{
    options.LoginPath = "/Usuarios/Login";
    options.AccessDeniedPath = "/Home/AccessDenied";
});

// Configurar autorización
builder.Services.AddAuthorization();

var app = builder.Build();

// Configuración de errores
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Configuración del sistema
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// IMPORTANTE: primero autenticación, luego autorización
app.UseAuthentication();
app.UseAuthorization();

// Ruta principal
app.MapControllerRoute(
name: "default",
pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();