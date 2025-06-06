﻿using MovieShop.Server.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;
using Microsoft.AspNetCore.Identity;

namespace MovieShop.Server.Data
{
    public class AppDbContext: IdentityDbContext<User, IdentityRole<int>, int> // : DbContext 
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Movie> Movies { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderMovie> OrderMovies { get; set; }
        // public DbSet<User> Users { get; set; }
        public DbSet<ShoppingCart> ShoppingCarts { get; set; }
        public DbSet<ShoppingCartMovie> ShoppingCartMovies { get; set; }
        public DbSet<Address> Addresses { get; set; }
        public DbSet<Review> Reviews { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<OrderMovie>()
                .HasKey(om => new { om.OrderId, om.MovieId });

            modelBuilder.Entity<ShoppingCartMovie>()
                .HasKey(scm => new { scm.ShoppingCartId, scm.MovieId });

            modelBuilder.Entity<Movie>()
                .HasMany(m => m.Categories)
                .WithMany(c => c.Movies)
                .UsingEntity<Dictionary<string, object>>(
                    "MovieCategory",
                    j => j.HasOne<Category>().WithMany().HasForeignKey("CategoryId").OnDelete(DeleteBehavior.Restrict),
                    j => j.HasOne<Movie>().WithMany().HasForeignKey("MovieId").OnDelete(DeleteBehavior.Restrict)
                );

            modelBuilder.Entity<OrderMovie>()
                .HasOne(om => om.Movie)
                .WithMany(m => m.OrderMovies)
                .HasForeignKey(om => om.MovieId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.BillingAddress)
                .WithMany(a => a.BillingOrders)
                .HasForeignKey(o => o.BillingAddressId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.ShippingAddress)
                .WithMany(a => a.ShippingOrders)
                .HasForeignKey(o => o.ShippingAddressId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.User)
                .WithMany(u => u.Reviews)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.Movie)
                .WithMany(m => m.Reviews)
                .HasForeignKey(r => r.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Movie>()
                .Property(m => m.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

        }

        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries<Movie>();

            foreach (var entry in entries)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = DateTime.UtcNow;
                        break;

                    case EntityState.Modified:
                        entry.Entity.UpdatedAt = DateTime.UtcNow;

                        bool originalIsDeleted = entry.OriginalValues.GetValue<bool>(nameof(Movie.IsDeleted));

                        if (entry.Entity.IsDeleted && !originalIsDeleted)
                        {
                            entry.Entity.DeletedAt = DateTime.UtcNow;
                        }
                        else if (!entry.Entity.IsDeleted && originalIsDeleted)
                        {
                            entry.Entity.DeletedAt = null;
                        }

                        break;
                }
            }
        }




    }
}
