using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VideoCallProject.Models
{
    public class ChatMessage
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int VideoCallId { get; set; }
        
        [Required]
        [StringLength(200)]
        public string SenderName { get; set; }
        
        [Required]
        [StringLength(50)]
        public string SenderType { get; set; } // "operator", "customer"
        
        [Required]
        public string Message { get; set; }
        
        [StringLength(50)]
        public string MessageType { get; set; } = "text"; // "text", "file", "system"
        
        public DateTime SentAt { get; set; }
        
        public bool IsRead { get; set; } = false;
        
        public bool IsDeleted { get; set; } = false;
        
        public DateTime? EditedAt { get; set; }
        
        public int? ReplyToMessageId { get; set; }

        // Navigation Properties
        [ForeignKey("VideoCallId")]
        public virtual VideoCall VideoCall { get; set; }
        
        [ForeignKey("ReplyToMessageId")]
        public virtual ChatMessage ReplyToMessage { get; set; }

        public ChatMessage()
        {
            SentAt = DateTime.Now;
        }
    }
}
