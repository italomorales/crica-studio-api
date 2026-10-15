using System.Text;
using CricaStudio.Api.Auth;
using CricaStudio.Api.Catalog;
using CricaStudio.Application.Auth;
using CricaStudio.Application.Catalog;
using CricaStudio.Domain.AdminUsers;
using CricaStudio.Domain.Catalog;
using CricaStudio.Infrastructure.Persistence;
using CricaStudio.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using CricaStudio.Api.Blog;
using CricaStudio.Domain.Blog;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
    var localSecrets = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    if (!string.IsNullOrWhiteSpace(localSecrets))
        builder.Configuration.AddJsonFile(Path.Combine(localSecrets, "CricaStudio", "Secrets", "blog-google.json"), optional: true, reloadOnChange: false);
}

var databaseConnectionString = builder.Configuration.GetConnectionString("Database");
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("A configuração Jwt é obrigatória.");
jwtSettings.Validate();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var catalogMediaOptions = builder.Configuration.GetSection(CatalogMediaOptions.SectionName).Get<CatalogMediaOptions>() ?? new();

builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();
builder.AddBlogSocial();
builder.Services.AddScoped<PostgresBlogActivityRepository>(_=>new(databaseConnectionString??throw new InvalidOperationException("Banco não configurado.")));

builder.Services.AddScoped<IAdminUserRepository>(_ => string.IsNullOrWhiteSpace(databaseConnectionString)
    ? throw new InvalidOperationException("Defina ConnectionStrings__Database no ambiente da API.")
    : new PostgresAdminUserRepository(databaseConnectionString));
builder.Services.AddScoped<ICatalogReadRepository>(_ => string.IsNullOrWhiteSpace(databaseConnectionString)
    ? throw new InvalidOperationException("Defina ConnectionStrings__Database no ambiente da API.")
    : new PostgresCatalogReadRepository(databaseConnectionString));
builder.Services.AddScoped<ICatalogWriteRepository>(_ => string.IsNullOrWhiteSpace(databaseConnectionString)
    ? throw new InvalidOperationException("Defina ConnectionStrings__Database no ambiente da API.")
    : new PostgresCatalogWriteRepository(databaseConnectionString));
builder.Services.AddSingleton(catalogMediaOptions);
builder.Services.AddScoped<ICatalogMediaStorage, S3CatalogMediaStorage>();
var localBlogMedia = (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("BlogTest")) && catalogMediaOptions.Provider == "local" && !string.IsNullOrWhiteSpace(catalogMediaOptions.LocalDirectory);
if (localBlogMedia) builder.Services.AddScoped<ICatalogMediaStorage, LocalBlogMediaStorage>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IBlogRepository>(_ => new PostgresBlogRepository(databaseConnectionString ?? throw new InvalidOperationException("Banco não configurado.")));
builder.Services.AddScoped<BlogService>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IAccessTokenService>(new JwtTokenService(jwtSettings));
builder.Services.AddScoped<LoginUseCase>();
builder.Services.AddScoped<CatalogReadService>();
builder.Services.AddScoped<IPlatformRepository>(_ => new PostgresPlatformRepository(databaseConnectionString ?? throw new InvalidOperationException("Banco não configurado.")));
builder.Services.AddScoped<ITranslationRepository>(_ => new PostgresTranslationRepository(databaseConnectionString ?? throw new InvalidOperationException("Banco não configurado.")));
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCors("Frontend");
// CloudFront overwrites the viewer host and supplies a private origin key. Never trust an unverified forwarded host.
app.Use(async(context,next)=>{
    var proxyKey=builder.Configuration["BlogSocial:ProxyKey"];
    if(context.Request.Path.StartsWithSegments("/api/blog") && !string.IsNullOrEmpty(proxyKey)){
        var incoming=context.Request.Headers["X-Crica-Proxy-Key"].ToString();
        if(System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(proxyKey),Encoding.UTF8.GetBytes(incoming))){
            var viewerHost=context.Request.Headers["X-Crica-Viewer-Host"].ToString();
            if(BlogSocialAuthentication.AllowedOrigin("https://"+viewerHost,builder.Configuration)){
                context.Request.Host=new HostString(viewerHost);context.Request.Scheme="https";
            }
        }
    }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
if (localBlogMedia)
{
    Directory.CreateDirectory(catalogMediaOptions.LocalDirectory);
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(Path.GetFullPath(catalogMediaOptions.LocalDirectory)), RequestPath = "/catalog/blog" });
}
app.MapHealthChecks("/health");
app.MapGet("/health/database", async (CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(databaseConnectionString))
    {
        return Results.Problem(title: "Banco de dados não configurado", detail: "Defina ConnectionStrings__Database no ambiente da API.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        await using var connection = new Npgsql.NpgsqlConnection(databaseConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new Npgsql.NpgsqlCommand("SELECT CURRENT_DATE", connection);
        var currentDate = await command.ExecuteScalarAsync(cancellationToken);
        return Results.Ok(new { status = "Healthy", currentDate });
    }
    catch (Npgsql.NpgsqlException)
    {
        return Results.Problem(title: "Banco de dados indisponível", detail: "Não foi possível conectar ou executar a consulta no banco de dados.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});
app.MapGet("/api/ping", () => Results.Ok(new { message = "API online", timestamp = DateTimeOffset.UtcNow }));
app.MapAuthEndpoints();
app.MapCatalogEndpoints();
app.MapAdminCatalogEndpoints();
app.MapPlatformEndpoints();
app.MapTranslationEndpoints();
app.MapBlogEndpoints();
app.MapBlogInteractions();
app.MapBlogHtmlEndpoints();
if (builder.Environment.IsEnvironment("BlogTest") && !string.IsNullOrWhiteSpace(builder.Configuration["Blog:FrontendDirectory"]))
{
    var frontend = Path.GetFullPath(builder.Configuration["Blog:FrontendDirectory"]!);
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(frontend) });
}

app.Run();

public partial class Program;
