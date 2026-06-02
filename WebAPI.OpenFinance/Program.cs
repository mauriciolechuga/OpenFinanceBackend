using Microsoft.EntityFrameworkCore;
using WebAPI.OpenFinance.Data;
using WebAPI.OpenFinance.Routes;

namespace WebAPI.OpenFinance
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // The connection string is supplied via user-secrets (Development) or environment
            // variables (other environments) — never committed to source control.
            builder.Services.AddDbContext<OpenFinanceContext>(options =>
            {
                options.UseNpgsql(builder.Configuration.GetConnectionString("DBOpenFinanceConnection"));
            });

            builder.Services.AddAuthorization();

            // Swagger / OpenAPI: https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // Feature endpoints, each registered via its own extension method.
            app.BanksListRoutes();
            app.ClientRoutes();
            app.AuthenticationRoutes();

            app.Run();
        }
    }
}
