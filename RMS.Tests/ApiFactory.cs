using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RMS.DAL;

namespace RMS.Tests
{
    public class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection connection;

        static ApiFactory()
        {
            Environment.SetEnvironmentVariable("AutoMigrate", "false");
            Environment.SetEnvironmentVariable("UseSqlite", "true");
        }

        public ApiFactory()
        {
            connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var remove = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<RmsDBContext>) ||
                    d.ServiceType == typeof(RmsDBContext)).ToList();

                foreach (var descriptor in remove)
                    services.Remove(descriptor);

                services.AddDbContext<RmsDBContext>(options => options.UseSqlite(connection));
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = base.CreateHost(builder);
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RmsDBContext>();
            db.Database.EnsureCreated();
            return host;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                connection.Dispose();
        }
    }
}
