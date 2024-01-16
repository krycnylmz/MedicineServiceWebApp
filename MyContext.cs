using Microsoft.EntityFrameworkCore;

using MedicineService.Models;

public class MyContext : DbContext
{
    public MyContext(DbContextOptions<MyContext> options)
        : base(options)
    {
    }

    // DbSet property for your entity (replace with the actual entity class)
    public DbSet<Medicine> Medicines { get; set; }
}
