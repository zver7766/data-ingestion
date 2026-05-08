using Microsoft.EntityFrameworkCore;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
});
builder.Services.AddScoped<DataIngestion.Services.Ingest.ITransactionIngestionService, DataIngestion.Services.Ingest.TransactionIngestionService>();
builder.Services.AddScoped<DataIngestion.Services.Ingest.TransactionDuplicateChecker>();
builder.Services.AddScoped<DataIngestion.Services.Ingest.BatchIngestionService>();
builder.Services.AddSingleton<DataIngestion.Services.Ingest.TransactionValidationService>();
builder.Services.AddScoped<DataIngestion.Services.Customers.CustomerTransactionsService>();
builder.Services.AddScoped<DataIngestion.Services.Stats.IStatsSummaryService, DataIngestion.Services.Stats.StatsSummaryService>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found. Set it in appsettings.json or via ConnectionStrings__DefaultConnection.");
}

builder.Services.AddDbContext<DataIngestion.Data.AppDbContext>(options =>
    options.UseSqlServer(connectionString));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DataIngestion.Data.AppDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();