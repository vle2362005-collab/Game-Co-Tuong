using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GameCoTuong.Infrastructure.Persistence;

public class GameDbContextFactory : IDesignTimeDbContextFactory<GameDbContext>
{
    public GameDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GameDbContext>();
        optionsBuilder.UseSqlServer("Server=.\\SQLEXPRESS;Database=GameCoTuong;Integrated Security=True;Encrypt=True;TrustServerCertificate=True");

        return new GameDbContext(optionsBuilder.Options);
    }
}
