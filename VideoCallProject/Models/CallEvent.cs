using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VideoCallProject.Models
{
    public class CallEvent
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int VideoCallId { get; set; }
        
        [Required]
        [StringLength(100)]
        public string EventType { get; set; } // "user_joined", "user_left", "call_started", etc.
        
        [StringLength(500)]
        public string EventDescription { get; set; }
        
        public string EventData { get; set; } // JSON format
        
        [StringLength(200)]
        public string ParticipantName { get; set; }
        
        public DateTime OccurredAt { get; set; }
        
        [StringLength(50)]
        public string Severity { get; set; } = "info"; // "info", "warning", "error", "critical"

        // Navigation Properties
        [ForeignKey("VideoCallId")]
        public virtual VideoCall VideoCall { get; set; }

        public CallEvent()
        {
            OccurredAt = DateTime.Now;
        }
    }
}
