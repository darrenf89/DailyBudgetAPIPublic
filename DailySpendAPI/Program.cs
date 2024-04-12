using DailyBudgetAPI.Data;
using DailyBudgetAPI.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Newtonsoft.Json.Serialization;
using Azure.Storage.Blobs;

try
{
    var builder = WebApplication.CreateBuilder(args);


    builder.Services.AddControllers().AddNewtonsoftJson(s =>
    {
        s.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
    });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Configuration.AddEnvironmentVariables().AddUserSecrets(Assembly.GetExecutingAssembly(), true);

    var sqlConBuilder = new SqlConnectionStringBuilder();
    sqlConBuilder.ConnectionString = builder.Configuration.GetConnectionString("DefaultConnectionString");

    Console.WriteLine(sqlConBuilder.ToString());

    sqlConBuilder.UserID = builder.Configuration["UserID"];
    sqlConBuilder.Password = builder.Configuration["Password"];

    builder.Services.AddDbContext<ApplicationDBContext>(options => options.UseSqlServer(sqlConBuilder.ConnectionString));
    builder.Services.AddScoped<ISecurityHelper, SecurityHelper>();
    builder.Services.AddScoped<IProductTools, ProductTools>();
    builder.Services.AddScoped<IEmailService, EmailService>();
    builder.Services.AddScoped<IFIleStorageService, FIleStorageService>();

    builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

    builder.Services.AddScoped(_ =>
    {        
        return new BlobServiceClient(builder.Configuration.GetValue<string>("Blob:ConnectionString") ?? "");
    });

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Console.Write(ex.Message + ex.StackTrace);
}
