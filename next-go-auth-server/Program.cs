using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using next_go_api.Seeders;
using next_go_api.Services;
using next_go_auth_server.Database;
using next_go_auth_server.Extensions;
using System;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services
    .AddIdentityCore<User>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

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

// Enable Identity APIs
//app.CustomMapIdentityApi<User>();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await IdentityRoleSeeder.SeedRolesAsync(services);
}

await IdentitySeeder.SeedAdminUserAsync<User>(app.Services);
builder.Services.AddAuthorization();


app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
