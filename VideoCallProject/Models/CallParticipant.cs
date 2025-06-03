using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VideoCallProject.Models
{
    public class CallParticipant
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int VideoCallId { get; set; }
        
        [Required]
        [StringLength(200)]
        public string ParticipantName { get; set; }
        
        [Required]
        [StringLength(50)]
        public string ParticipantType { get; set; } // "operator", "customer"
        
        public int? ParticipantId { get; set; }
        
        [StringLength(200)]
        public string ConnectionId { get; set; }
        
        public DateTime JoinedAt { get; set; }
        
        public DateTime? LeftAt { get; set; }
        
        public int Duration { get; set; } = 0; // seconds
        
        [StringLength(50)]
        public string IpAddress { get; set; }
        
        [StringLength(500)]
        public string UserAgent { get; set; }
        
        [StringLength(100)]
        public string DeviceType { get; set; } // "desktop", "mobile", "tablet"
        
        [StringLength(100)]
        public string BrowserName { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        [StringLength(200)]
        public string DisconnectReason { get; set; }

        // Navigation Properties
        [ForeignKey("VideoCallId")]
        public virtual VideoCall VideoCall { get; set; }

        public CallParticipant()
        {
            JoinedAt = DateTime.Now;
        }
    }
}
