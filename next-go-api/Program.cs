using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using next_go_api.Database;
using next_go_api.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddAuthorization();
builder.Services.AddAuthentication().AddCookie(IdentityConstants.ApplicationScheme);

builder.Services.AddIdentityCore<User>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddApiEndpoints();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(
          builder.Configuration.GetConnectionString("DefaultConnection"),
          x => x.MigrationsHistoryTable("__EFMigrationsHistory", "identity")
      );
    options.EnableSensitiveDataLogging();                        
});
var app = builder.Build();

// Configure the HTTP request pipeline.     
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    app.ApplyMigrations();
}

app.UseHttpsRedirection();

//app.UseAuthorization();

app.MapControllers();

app.Run();
