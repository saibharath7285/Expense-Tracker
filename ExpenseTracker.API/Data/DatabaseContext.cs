using ExpenseTracker.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.API.Data
{
    public class DatabaseContext : DbContext
    {
        public DatabaseContext(DbContextOptions<DatabaseContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Expense_Result>(entity =>
            {
                entity.HasNoKey();
                entity.Property(e => e.Amount).HasPrecision(18, 2);
            });
            modelBuilder.Entity<GetCategories_Result>(e => e.HasNoKey());
            modelBuilder.Entity<GetSpendingSummary_Result>(e => e.HasNoKey());
            modelBuilder.Entity<AddExpense_Result>(e => e.HasNoKey());
            modelBuilder.Entity<NoOutputResult>(e => e.HasNoKey());
        }
    }
}
