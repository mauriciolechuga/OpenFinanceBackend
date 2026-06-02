using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using WebAPI.OpenFinance.Aggregation;
using WebAPI.OpenFinance.Auth;
using WebAPI.OpenFinance.BackgroundServices;
using WebAPI.OpenFinance.Data;
using WebAPI.OpenFinance.Routes;
using WebAPI.OpenFinance.Services;
using WebAPI.OpenFinance.Validation;

namespace WebAPI.OpenFinance
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // --- Persistence ---
            // Connection string comes from user-secrets (dev) or environment (prod), never a committed file.
            builder.Services.AddDbContext<OpenFinanceContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DBOpenFinanceConnection")));

            // --- Auth (self-hosted JWT) ---
            var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
            if (string.IsNullOrWhiteSpace(jwtSettings.Key))
            {
                if (builder.Environment.IsDevelopment())
                {
                    // Dev-only fallback so the app runs without setup. Production refuses to start without a real key.
                    jwtSettings.Key = "dev-only-insecure-signing-key-do-not-use-in-production-0123456789";
                }
                else
                {
                    throw new InvalidOperationException(
                        "Jwt:Key must be configured via user-secrets or environment variables outside Development.");
                }
            }
            builder.Services.AddSingleton(Options.Create(jwtSettings));

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.MapInboundClaims = false; // keep the raw "sub" claim
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtSettings.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwtSettings.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1)
                    };
                });
            builder.Services.AddAuthorization();

            // --- Cross-cutting ---
            builder.Services.AddProblemDetails();
            builder.Services.AddDataProtection();
            builder.Services.AddValidatorsFromAssemblyContaining<SignupRequestValidator>();

            // --- Application services ---
            builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IPortfolioService, PortfolioService>();
            builder.Services.AddScoped<IAggregationService, AggregationService>();
            builder.Services.AddSingleton<ITokenProtector, DataProtectionTokenProtector>();

            // Aggregation provider: the $0 mock today; swap/add real providers behind this registration.
            builder.Services.AddSingleton<IAggregationProvider, MockAggregationProvider>();
            builder.Services.AddHostedService<AggregationSyncHostedService>();

            // --- Swagger (with bearer auth) ---
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                var scheme = new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                };
                options.AddSecurityDefinition("Bearer", scheme);
                options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
            });

            var app = builder.Build();

            // --- Pipeline ---
            app.UseExceptionHandler(); // unhandled exceptions become ProblemDetails responses

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();

            // Feature endpoints.
            app.BanksListRoutes();
            app.ClientRoutes();
            app.AuthenticationRoutes();
            app.AggregationRoutes();

            app.Run();
        }
    }
}
