using Microsoft.EntityFrameworkCore;

namespace TradingBot.ApiService.Persistent;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions options) : base(options)
    {}
}