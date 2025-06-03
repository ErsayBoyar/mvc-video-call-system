using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;

namespace VideoCallProject.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext() : base("DefaultConnection")
        {
            Database.SetInitializer<ApplicationDbContext>(null);
        }

        public DbSet<VideoCall> VideoCalls { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<FileShare> FileShares { get; set; }
        public DbSet<CallParticipant> CallParticipants { get; set; }
        public DbSet<CallEvent> CallEvents { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();

            // VideoCall Configuration
            modelBuilder.Entity<VideoCall>()
                .HasIndex(e => e.RoomId)
                .HasName("IX_VideoCalls_RoomId");

            modelBuilder.Entity<VideoCall>()
                .HasIndex(e => e.TalepId)
                .HasName("IX_VideoCalls_TalepId");

            modelBuilder.Entity<VideoCall>()
                .HasIndex(e => e.Status)
                .HasName("IX_VideoCalls_Status");

            // ChatMessage Configuration
            modelBuilder.Entity<ChatMessage>()
                .HasRequired(c => c.VideoCall)
                .WithMany(v => v.ChatMessages)
                .HasForeignKey(c => c.VideoCallId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<ChatMessage>()
                .HasOptional(c => c.ReplyToMessage)
                .WithMany()
                .HasForeignKey(c => c.ReplyToMessageId)
                .WillCascadeOnDelete(false);

            // FileShare Configuration
            modelBuilder.Entity<FileShare>()
                .HasRequired(f => f.VideoCall)
                .WithMany(v => v.FileShares)
                .HasForeignKey(f => f.VideoCallId)
                .WillCascadeOnDelete(true);

            // CallParticipant Configuration
            modelBuilder.Entity<CallParticipant>()
                .HasRequired(p => p.VideoCall)
                .WithMany(v => v.Participants)
                .HasForeignKey(p => p.VideoCallId)
                .WillCascadeOnDelete(true);

            // CallEvent Configuration
            modelBuilder.Entity<CallEvent>()
                .HasRequired(e => e.VideoCall)
                .WithMany(v => v.Events)
                .HasForeignKey(e => e.VideoCallId)
                .WillCascadeOnDelete(true);

            base.OnModelCreating(modelBuilder);
        }
    }
}
