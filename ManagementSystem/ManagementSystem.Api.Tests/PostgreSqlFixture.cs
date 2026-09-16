using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using ManagementSystem.Api.Data;
using ManagementSystem.Api.Services;
using Xunit;

namespace ManagementSystem.Api.Tests
{
    public class PostgreSqlFixture : IAsyncLifetime
    {
        private PostgreSqlContainer? _container;

        public bool IsDockerAvailable { get; private set; }
        public string? DockerError { get; private set; }
        public string? ConnectionString { get; private set; }
        public IDbContextFactory<AppDbContext>? ContextFactory { get; private set; }
        public IServiceProvider? ServiceProvider { get; private set; }

        public async Task InitializeAsync()
        {
            try
            {
                _container = new PostgreSqlBuilder()
                    .WithImage("postgres:16-alpine")
                    .WithDatabase("test_managementsystem")
                    .WithUsername("postgres")
                    .WithPassword("test_password")
                    .Build();

                await _container.StartAsync();
                ConnectionString = _container.GetConnectionString();

                var services = new ServiceCollection();
                services.AddDbContextFactory<AppDbContext>(options =>
                    options.UseNpgsql(ConnectionString));
                services.AddDbContext<AppDbContext>(options =>
                    options.UseNpgsql(ConnectionString), ServiceLifetime.Scoped, ServiceLifetime.Singleton);
                services.AddScoped<IAttendanceService, AttendanceService>();
                services.AddHttpContextAccessor();

                ServiceProvider = services.BuildServiceProvider();
                ContextFactory = ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

                // Apply migrations to prepare schema
                await using var context = await ContextFactory.CreateDbContextAsync();
                await context.Database.MigrateAsync();

                IsDockerAvailable = true;
            }
            catch (Exception ex)
            {
                IsDockerAvailable = false;
                DockerError = ex.Message;
            }
        }

        public void EnsureDockerOrSkip()
        {
            Skip.If(!IsDockerAvailable, $"PostgreSQL integration test is blocked: Docker/PostgreSql container is unavailable ({DockerError}).");
        }

        public async Task<AppDbContext> CreateContextAsync()
        {
            EnsureDockerOrSkip();
            return await ContextFactory!.CreateDbContextAsync();
        }

        public IAttendanceService CreateAttendanceService()
        {
            EnsureDockerOrSkip();
            return new AttendanceService(ContextFactory!);
        }

        public async Task DisposeAsync()
        {
            if (_container != null)
            {
                try
                {
                    await _container.DisposeAsync();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
        }
    }
}
