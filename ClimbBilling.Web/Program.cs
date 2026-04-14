using ClimbBilling.Web.Data;
using ClimbBilling.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ----------------------------------------------------------------
// Database
// ----------------------------------------------------------------
builder.Services.AddDbContext<BillingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("BillingDb")));

// ----------------------------------------------------------------
// Authentication
// Shared Climb TMS identity — configurable via appsettings.
// For local dev without TMS, a cookie-based dev stub is used.
// ----------------------------------------------------------------
var oidcConfig = builder.Configuration.GetSection("Authentication:Oidc");
if (oidcConfig.Exists() && !string.IsNullOrEmpty(oidcConfig["Authority"]))
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.Authority = oidcConfig["Authority"];
        options.ClientId = oidcConfig["ClientId"];
        options.ClientSecret = oidcConfig["ClientSecret"];
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
    });
}
else
{
    // Dev mode: simple cookie auth (user can log in without a real IdP)
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.LoginPath = "/Account/Login";
        });
}

// ----------------------------------------------------------------
// Application services
// ----------------------------------------------------------------
builder.Services.AddScoped<StripeService>();
builder.Services.AddScoped<InvoiceService>();

// ----------------------------------------------------------------
// MVC
// ----------------------------------------------------------------
builder.Services.AddControllersWithViews();

var app = builder.Build();

// ----------------------------------------------------------------
// Auto-migrate on startup (development convenience)
// Remove for production — use proper migration pipeline
// ----------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
    db.Database.Migrate();
}

// ----------------------------------------------------------------
// Middleware
// ----------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Stripe webhook must bypass CSRF — registered first
app.MapControllerRoute(
    name: "stripe",
    pattern: "stripe/{action}",
    defaults: new { controller = "StripeWebhook" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
