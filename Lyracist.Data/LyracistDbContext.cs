using Lyracist.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Lyracist.Data
{
    public class LyracistDbContext : DbContext
    {
        public DbSet<Song> Songs { get; set; } = null!;
        public DbSet<SongAudioSettings> SongAudioSettings { get; set; } = null!;
        public DbSet<Singer> Singers { get; set; } = null!;
        public DbSet<SingerAudioSettings> SingerAudioSettings { get; set; } = null!;
        public DbSet<RotationEntry> RotationEntries { get; set; } = null!;
        public DbSet<MusicRequest> MusicRequests { get; set; } = null!;
        public DbSet<OpeningPlaylistItem> OpeningPlaylistItems { get; set; } = null!;
        public DbSet<FillInPlaylistItem> FillInPlaylistItems { get; set; } = null!;
        public DbSet<EndRotationPlaylistItem> EndRotationPlaylistItems { get; set; } = null!;
        public DbSet<OccasionCategory> OccasionCategories { get; set; } = null!;
        public DbSet<OccasionItem> OccasionItems { get; set; } = null!;
        public DbSet<SongSearch> SongSearches { get; set; } = null!;

        public LyracistDbContext()
        {
        }

        public LyracistDbContext(DbContextOptions<LyracistDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                string baseDir = System.AppDomain.CurrentDomain.BaseDirectory;
                string dataDir = System.IO.Path.Combine(baseDir, "Data");
                System.IO.Directory.CreateDirectory(dataDir);
                string dbPath = System.IO.Path.Combine(dataDir, "lyracist.db");
                optionsBuilder.UseSqlite($"Data Source={dbPath};Cache=Shared");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Song Indexes
            modelBuilder.Entity<Song>(entity =>
            {
                entity.HasIndex(s => s.Title);
                entity.HasIndex(s => s.Artist);
                entity.HasIndex(s => s.FilePath);
            });

            // Song <-> SongAudioSettings (1-to-1)
            modelBuilder.Entity<Song>()
                .HasOne(s => s.AudioSettings)
                .WithOne(a => a.Song)
                .HasForeignKey<SongAudioSettings>(a => a.SongId)
                .OnDelete(DeleteBehavior.Cascade);

            // Singer <-> SingerAudioSettings (1-to-1)
            modelBuilder.Entity<Singer>()
                .HasOne(s => s.AudioSettings)
                .WithOne(a => a.Singer)
                .HasForeignKey<SingerAudioSettings>(a => a.SingerId)
                .OnDelete(DeleteBehavior.Cascade);

            // RotationEntry (FK relations)
            modelBuilder.Entity<RotationEntry>(entity =>
            {
                entity.HasOne(r => r.Singer)
                    .WithMany(s => s.RotationEntries)
                    .HasForeignKey(r => r.SingerId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.Song)
                    .WithMany(s => s.RotationEntries)
                    .HasForeignKey(r => r.SongId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // MusicRequest
            modelBuilder.Entity<MusicRequest>()
                .HasOne(m => m.Singer)
                .WithMany(s => s.MusicRequests)
                .HasForeignKey(m => m.SingerId)
                .OnDelete(DeleteBehavior.Cascade);

            // OpeningPlaylistItem
            modelBuilder.Entity<OpeningPlaylistItem>()
                .HasOne(p => p.Song)
                .WithMany()
                .HasForeignKey(p => p.SongId)
                .OnDelete(DeleteBehavior.Cascade);

            // FillInPlaylistItem
            modelBuilder.Entity<FillInPlaylistItem>()
                .HasOne(p => p.Song)
                .WithMany()
                .HasForeignKey(p => p.SongId)
                .OnDelete(DeleteBehavior.Cascade);

            // EndRotationPlaylistItem
            modelBuilder.Entity<EndRotationPlaylistItem>()
                .HasOne(p => p.Song)
                .WithMany()
                .HasForeignKey(p => p.SongId)
                .OnDelete(DeleteBehavior.Cascade);

            // OccasionCategory (Self-referencing & Items)
            modelBuilder.Entity<OccasionCategory>(entity =>
            {
                entity.HasOne(c => c.ParentCategory)
                    .WithMany(c => c.SubCategories)
                    .HasForeignKey(c => c.ParentCategoryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // OccasionItem
            modelBuilder.Entity<OccasionItem>()
                .HasOne(i => i.OccasionCategory)
                .WithMany(c => c.Items)
                .HasForeignKey(i => i.OccasionCategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // SongSearch (FTS5 Virtual Table Configuration)
            modelBuilder.Entity<SongSearch>(entity =>
            {
                entity.HasKey(s => s.SongId);
                entity.ToTable("SongSearch", tb => tb.UseSqlReturningClause(false));
            });

            // Seeding default OccasionCategory entries
            modelBuilder.Entity<OccasionCategory>().HasData(
                new OccasionCategory { OccasionCategoryId = 1, Name = "Holiday" },
                new OccasionCategory { OccasionCategoryId = 2, Name = "Birthday" },
                new OccasionCategory { OccasionCategoryId = 3, Name = "Anniversary" },
                new OccasionCategory { OccasionCategoryId = 4, Name = "Wedding" },
                new OccasionCategory { OccasionCategoryId = 5, Name = "Graduation" },
                new OccasionCategory { OccasionCategoryId = 6, Name = "Retirement" }
            );
        }
    }
}
