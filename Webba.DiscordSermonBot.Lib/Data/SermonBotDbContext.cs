using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Webba.DiscordSermonBot.Lib.Models;

namespace Webba.DiscordSermonBot.Lib.Data
{
    public class SermonBotDbContext : DbContext
    {
        public SermonBotDbContext(DbContextOptions<SermonBotDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SermonRotation>()
                .HasMany<SermonMember>()
                .WithOne()
                .HasForeignKey(sm => sm.RotationId);
        }

        public DbSet<SermonRotation> Rotations { get; set; }
        public DbSet<SermonMember> Members { get; set; }
    }
}
