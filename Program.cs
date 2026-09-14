var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
}

var databaseConnectionString = builder.Configuration.GetConnectionString("Database");

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCors("Frontend");
app.MapHealthChecks("/health");
app.MapGet("/health/database", async (CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(databaseConnectionString))
    {
        return Results.Problem(
            title: "Banco de dados não configurado",
            detail: "Defina ConnectionStrings__Database no ambiente da API.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        await using var connection = new Npgsql.NpgsqlConnection(databaseConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new Npgsql.NpgsqlCommand("SELECT CURRENT_DATE", connection);
        var currentDate = await command.ExecuteScalarAsync(cancellationToken);

        return Results.Ok(new
        {
            status = "Healthy",
            currentDate,
        });
    }
    catch (Npgsql.NpgsqlException)
    {
        return Results.Problem(
            title: "Banco de dados indisponível",
            detail: "Não foi possível conectar ou executar a consulta no banco de dados.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});
app.MapGet("/api/ping", () => Results.Ok(new
{
    message = "API online",
    timestamp = DateTimeOffset.UtcNow,
}));

app.Run();
