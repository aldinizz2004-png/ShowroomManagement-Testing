using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Showroom.Api.Auth;
using Showroom.Api.Data;
using Showroom.Api.Models;
using Showroom.Api.Repositories;
using Showroom.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ===================== Services =====================

// Controllers; enums as text in JSON ("Chery", "Available")
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Database (SQL Server). The integration tests replace this with SQLite in-memory.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Data access (repositories) and business logic (services)
builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IVehicleService, VehicleService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Authentication: JWT Bearer. Options are read lazily, so tests can override the configuration.
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
builder.Services.AddScoped<ITokenService, TokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        options.MapInboundClaims = false;   // keep short claim names: sub, email, name, role
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            NameClaimType = "name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

// Authorization: the same policies are used by the unit tests (Policies.AddShowroomPolicies)
builder.Services.AddAuthorization(options => options.AddShowroomPolicies());

// RFC 7807 ProblemDetails for 400 / 401 / 403 / 404 / 500
builder.Services.AddProblemDetails();

// Swagger with the "Authorize" button for Bearer tokens
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Showroom Management API",
        Version = "v1",
        Description = "Vehicle inventory for Chery, OMODA and JAECOO with JWT and role-based access (Admin, SalesRep, Customer)."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste the token returned by POST /api/auth/login"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

// Create / migrate the database and the staff accounts (see Data/DbInitializer.cs)
await DbInitializer.InitializeAsync(app.Services);

// ===================== Pipeline =====================

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();   // who are you?  (validates the JWT)
app.UseAuthorization();    // are you allowed? (policies / roles)

app.MapControllers();

app.Run();

// Lets the integration tests use WebApplicationFactory<Program>
public partial class Program;
