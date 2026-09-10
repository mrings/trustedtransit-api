using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using TrustedTransit.Api.Data;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Get Auth0 settings
var auth0Domain = builder.Configuration["Auth0:Domain"];
var auth0Audience = builder.Configuration["Auth0:Audience"];

// Add services
builder.Services.AddControllers(options =>
{
    // Non-nullable reference types are not implicitly [Required]; validate explicitly with [Required].
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});


// Add Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Convert postgresql:// URL to Npgsql connection string if needed
if (connectionString?.StartsWith("postgresql://") == true)
{
    var uri = new Uri(connectionString);
    connectionString = $"Server={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};User Id={uri.UserInfo.Split(':')[0]};Password={uri.UserInfo.Split(':')[1]};SslMode=Require;";
}

builder.Services.AddDbContext<TrustedTransitDbContext>(options =>
    options.UseNpgsql(connectionString)
);

// Add Authentication
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://{auth0Domain}/";
        options.Audience = auth0Audience;
        
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
        };
    });

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Build
var app = builder.Build();

// Middleware

// Return a real JSON error (with CORS headers) instead of a bare connection drop,
// so client-side errors aren't masked as opaque CORS failures. Logs the full
// exception to stdout (visible in Railway logs).
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);

    // A DB constraint failure (bad FK, duplicate) is a client-data problem, not a server bug.
    var isDbConstraint = ex is DbUpdateException && ex.InnerException is PostgresException pg
        && (pg.SqlState == PostgresErrorCodes.ForeignKeyViolation || pg.SqlState == PostgresErrorCodes.UniqueViolation);

    context.Response.StatusCode = isDbConstraint ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/problem+json";
    context.Response.Headers.AccessControlAllowOrigin = "*";
    await context.Response.WriteAsJsonAsync(new
    {
        type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
        title = isDbConstraint
            ? "That change references a record that doesn't exist, or one that already exists."
            : "An unexpected error occurred.",
        status = context.Response.StatusCode,
        // Only leak exception text in development.
        detail = app.Environment.IsDevelopment() ? ex?.GetBaseException().Message : null
    });
}));

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TrustedTransitDbContext>();
    db.Database.Migrate();
}

app.Run();