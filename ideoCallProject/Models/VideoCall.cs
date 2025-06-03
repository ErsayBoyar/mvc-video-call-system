using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VideoCallProject.Models
{
    public class VideoCall
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string RoomId { get; set; }
        
        [Required]
        public int TalepId { get; set; }
        
        public int? OperatorId { get; set; }
        
        [StringLength(200)]
        public string CallerName { get; set; }
        
        [StringLength(50)]
        public string CallerType { get; set; } // "operator", "customer"
        
        [StringLength(500)]
        public string Subject { get; set; }
        
        [Required]
        public DateTime StartTime { get; set; }
        
        public DateTime? EndTime { get; set; }
        
        public int Duration { get; set; } = 0; // seconds
        
        [StringLength(50)]
        public string Status { get; set; } = "active"; // "active", "ended", "failed", "cancelled"
        
        [StringLength(50)]
        public string ConnectionQuality { get; set; } = "good";
        
        public bool IsRecorded { get; set; } = false;
        
        [StringLength(1000)]
        public string RecordingPath { get; set; }
        
        public long? RecordingSize { get; set; }
        
        public int ParticipantCount { get; set; } = 0;
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime? UpdatedAt { get; set; }

        // Navigation Properties
        public virtual ICollection<ChatMessage> ChatMessages { get; set; }
        public virtual ICollection<FileShare> FileShares { get; set; }
        public virtual ICollection<CallParticipant> Participants { get; set; }
        public virtual ICollection<CallEvent> Events { get; set; }

        public VideoCall()
        {
            ChatMessages = new HashSet<ChatMessage>();
            FileShares = new HashSet<FileShare>();
            Participants = new HashSet<CallParticipant>();
            Events = new HashSet<CallEvent>();
            StartTime = DateTime.Now;
            CreatedAt = DateTime.Now;
        }
    }
}
