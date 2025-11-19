using ClaimSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace ClaimSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Claim> Claims { get; set; }
    }
}