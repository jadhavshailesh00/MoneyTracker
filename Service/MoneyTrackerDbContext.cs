using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Budget
{ 
    public class MoneyTrackerDbContext : DbContext
    {   
        public MoneyTrackerDbContext(DbContextOptions<MoneyTrackerDbContext> options) : base(options) { }

        public DbSet<Entity.User> Users { get; set; }
        public DbSet<Entity.Account> Accounts { get; set; }
        public DbSet<Entity.Category> Categories { get; set; }
        public DbSet<Entity.Transaction> Transactions { get; set; }
        public DbSet<Entity.TransactionCategory> TransactionCategories { get; set; }
        public DbSet<Entity.Budget> Budgets { get; set; }
        public DbSet<Entity.ImportBatch> ImportBatches { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User
            modelBuilder.Entity<Entity.User>(entity =>
            {
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
                entity.HasIndex(e => e.Email).IsUnique();
            });

            // Account
            modelBuilder.Entity<Entity.Account>(entity =>
            {
                entity.HasKey(e => e.AccountId);
                entity.Property(e => e.AccountName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.AccountType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.CurrencyCode).IsRequired().HasMaxLength(3);
                entity.Property(e => e.OpeningBalance).HasPrecision(18, 2).HasDefaultValue(0);
                entity.Property(e => e.IsArchived).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
                
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Accounts)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasIndex(e => e.UserId);
            });

            // Category
            modelBuilder.Entity<Entity.Category>(entity =>
            {
                entity.HasKey(e => e.CategoryId);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Type).IsRequired().HasMaxLength(20);
                
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Categories)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.ParentCategory)
                    .WithMany(c => c.SubCategories)
                    .HasForeignKey(e => e.ParentCategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Transaction
            modelBuilder.Entity<Entity.Transaction>(entity =>
            {
                entity.HasKey(e => e.TransactionId);
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.Property(e => e.TransactionType).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Description).HasMaxLength(255);
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
                
                entity.HasOne(e => e.Account)
                    .WithMany(a => a.Transactions)
                    .HasForeignKey(e => e.AccountId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Transactions)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.ImportBatch)
                    .WithMany(i => i.Transactions)
                    .HasForeignKey(e => e.ImportBatchId)
                    .OnDelete(DeleteBehavior.SetNull);
                
                entity.HasIndex(e => new { e.UserId, e.TransactionDate }).IsDescending(false, true);
                entity.HasIndex(e => e.AccountId);
            });

            // TransactionCategory (Many-to-Many)
            modelBuilder.Entity<Entity.TransactionCategory>(entity =>
            {
                entity.HasKey(e => new { e.TransactionId, e.CategoryId });
                
                entity.HasOne(e => e.Transaction)
                    .WithMany(t => t.TransactionCategories)
                    .HasForeignKey(e => e.TransactionId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.Category)
                    .WithMany(c => c.TransactionCategories)
                    .HasForeignKey(e => e.CategoryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Budget
            modelBuilder.Entity<Entity.Budget>(entity =>
            {
                entity.HasKey(e => e.BudgetId);
                entity.Property(e => e.AmountLimit).HasPrecision(18, 2);
                entity.Property(e => e.Period).IsRequired().HasMaxLength(20);
                
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Budgets)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.Category)
                    .WithMany(c => c.Budgets)
                    .HasForeignKey(e => e.CategoryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ImportBatch
            modelBuilder.Entity<Entity.ImportBatch>(entity =>
            {
                entity.HasKey(e => e.ImportBatchId);
                entity.Property(e => e.Source).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ImportedAt).HasDefaultValueSql("SYSUTCDATETIME()");
                
                entity.HasOne(e => e.User)
                    .WithMany(u => u.ImportBatches)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}