using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SaborAQuisqueya.Data;
using SaborAQuisqueya.Services;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. CONFIGURAR SERVICIOS MVC
// ==========================================

builder.Services.AddControllersWithViews();


// ==========================================
// 2. CONFIGURAR SQL SERVER
// ==========================================

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);


// ==========================================
// 3. CONFIGURAR AUTENTICACIÓN POR COOKIES
// ==========================================

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Usuarios/Login";
        options.AccessDeniedPath = "/Home/AccessDenied";
    });


// ==========================================
// 4. CONFIGURAR AUTORIZACIÓN
// ==========================================

builder.Services.AddAuthorization();

builder.Services.AddTransient<EmailService>();
builder.Services.AddTransient<IEmailService, EmailService>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddScoped<RecuperacionContrasenaService>();
// ==========================================
// 5. REGISTRAR SERVICIO DE CORREO
// ==========================================

// Permite utilizar EmailService en los controladores.
builder.Services.AddTransient<EmailService>();

// Permite inyectar el servicio de recuperación de contraseña.
builder.Services.AddScoped<RecuperacionContrasenaService>();

var app = builder.Build();



// ==========================================
// 6. CONFIGURACIÓN DE ERRORES
// ==========================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}


// ==========================================
// 7. CONFIGURACIÓN DEL SISTEMA
// ==========================================

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();


// IMPORTANTE: autenticación antes de autorización
app.UseAuthentication();

app.UseAuthorization();
 

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();