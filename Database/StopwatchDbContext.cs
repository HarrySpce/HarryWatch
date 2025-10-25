using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Harry.Database
{
    public class HarryDBC : DbContext
    {
        private const string _connectionString = "Data Source=data.db";

        public DbSet<DbPacket> Packets { get; set; }
        public DbSet<DbLog> Log { get; set; }

        public HarryDBC()
        {
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options
            .UseSqlite(_connectionString)
            .EnableSensitiveDataLogging();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DbPacket>().ToTable("packets");
            modelBuilder.Entity<DbPacket>().HasIndex(c => c.CreatedAt);
            modelBuilder.Entity<DbLog>().ToTable("log");
            modelBuilder.Entity<DbLog>().HasIndex(m => m.CreatedAt);
        }
    }
}
