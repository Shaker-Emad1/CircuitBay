using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CircuitBay.Infrastructure.Persistence;

public sealed class CircuitBayDbContextFactory : IDesignTimeDbContextFactory<CircuitBayDbContext>
{
    public CircuitBayDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CircuitBayDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=CircuitBay;User Id=sa;Password=Strong_dev_password_2026!;TrustServerCertificate=True;MultipleActiveResultSets=True")
            .Options;

        return new CircuitBayDbContext(options);
    }
}
