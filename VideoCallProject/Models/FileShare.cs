using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VideoCallProject.Models
{
    public class FileShare
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
        [StringLength(500)]
        public string FileName { get; set; }
        
        [Required]
        [StringLength(500)]
        public string OriginalFileName { get; set; }
        
        [Required]
        [StringLength(1000)]
        public string FilePath { get; set; }
        
        [Required]
        public long FileSize { get; set; }
        
        [StringLength(100)]
        public string FileType { get; set; } // "image", "document", "video", "audio", "other"
        
        [StringLength(200)]
        public string MimeType { get; set; }
        
        [StringLength(64)]
        public string FileHash { get; set; }
        
        public DateTime SharedAt { get; set; }
        
        public bool IsDownloaded { get; set; } = false;
        
        public int DownloadCount { get; set; } = 0;
        
        public DateTime? ExpiresAt { get; set; }
        
        public bool IsDeleted { get; set; } = false;

        // Navigation Properties
        [ForeignKey("VideoCallId")]
        public virtual VideoCall VideoCall { get; set; }

        public FileShare()
        {
            SharedAt = DateTime.Now;
        }
    }
}
