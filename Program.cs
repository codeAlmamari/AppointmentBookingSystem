using AppointmentBookingSystem.Data;
using AppointmentBookingSystem.Helpers;
using AppointmentBookingSystem.Middleware;
using AppointmentBookingSystem.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using System.Reflection;

namespace AppointmentBookingSystem
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });
            builder.Services.AddOpenApi();

            // Database
            builder.Services.AddDbContext<AppDbContext>(opt =>
                opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

            // AutoMapper
            builder.Services.AddAutoMapper(cfg => { }, typeof(MappingProfile));

            // Services
            builder.Services.AddScoped<AuditService>();
            builder.Services.AddScoped<FileStorageService>();
            builder.Services.AddScoped<SeedImporter>();   

            // Bonus #3 — Rate limiting (singleton: shared across all requests)
            builder.Services.AddSingleton<RateLimitService>();

            // Bonus #4 — Background cron for auto cleanup
            builder.Services.AddHostedService<SlotCleanupBackgroundService>();

            var app = builder.Build();

            // Run migrations + seed on startup 
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var seeder = scope.ServiceProvider.GetRequiredService<SeedImporter>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                try
                {
                    await db.Database.MigrateAsync();
                    logger.LogInformation("Database migration applied.");
                    await seeder.RunAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Startup error during migration or seeding.");
                }
            }

            
            app.MapOpenApi();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/openapi/v1.json", "FlowCare API v1");
                c.RoutePrefix = "swagger";
            });
            

            if (!app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }
            app.UseCors();
            app.UseMiddleware<BasicAuthMiddleware>();
            app.MapControllers();

            app.Run();
        }
    }
}