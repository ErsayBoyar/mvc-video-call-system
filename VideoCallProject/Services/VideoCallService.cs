using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using Newtonsoft.Json;
using VideoCallProject.Models;

namespace VideoCallProject.Services
{
    public class VideoCallService : IVideoCallService
    {
        private readonly ApplicationDbContext _context;

        public VideoCallService()
        {
            _context = new ApplicationDbContext();
        }

        public VideoCallService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<VideoCall> StartVideoCallAsync(string roomId, int talepId, int? operatorId, string callerName, string callerType, string subject)
        {
            try
            {
                // Aynı RoomId ile aktif görüşme var mı kontrol et
                var existingCall = await _context.VideoCalls
                    .FirstOrDefaultAsync(x => x.RoomId == roomId && x.Status == "active");

                if (existingCall != null)
                {
                    return existingCall;
                }

                // Yeni görüşme oluştur
                var videoCall = new VideoCall
                {
                    RoomId = roomId,
                    TalepId = talepId,
                    OperatorId = operatorId,
                    CallerName = callerName,
                    CallerType = callerType,
                    Subject = subject,
                    StartTime = DateTime.Now,
                    Status = "active",
                    CreatedAt = DateTime.Now
                };

                _context.VideoCalls.Add(videoCall);
                await _context.SaveChangesAsync();

                // Event kaydı
                await LogEventAsync(roomId, "call_started", "Video call started", callerName);

                return videoCall;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting video call: {ex.Message}");
                throw;
            }
        }

        public async Task<VideoCall> EndVideoCallAsync(string roomId)
        {
            try
            {
                var videoCall = await _context.VideoCalls
                    .Include(x => x.Participants)
                    .FirstOrDefaultAsync(x => x.RoomId == roomId && x.Status == "active");

                if (videoCall == null)
                {
                    return null;
                }

                // Duration hesapla
                var duration = (int)(DateTime.Now - videoCall.StartTime).TotalSeconds;

                // Görüşmeyi sonlandır
                videoCall.EndTime = DateTime.Now;
                videoCall.Duration = duration;
                videoCall.Status = "ended";
                videoCall.UpdatedAt = DateTime.Now;

                // Aktif katılımcıları çıkar
                var activeParticipants = videoCall.Participants.Where(p => p.IsActive).ToList();
                foreach (var participant in activeParticipants)
                {
                    participant.LeftAt = DateTime.Now;
                    participant.Duration = (int)(DateTime.Now - participant.JoinedAt).TotalSeconds;
                    participant.IsActive = false;
                }

                await _context.SaveChangesAsync();

                // Event kaydı
                await LogEventAsync(roomId, "call_ended", "Video call ended");

                return videoCall;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error ending video call: {ex.Message}");
                throw;
            }
        }

        public async Task<ChatMessage> SaveChatMessageAsync(string roomId, string senderName, string senderType, string message, string messageType = "text")
        {
            try
            {
                var videoCall = await GetActiveCallByRoomIdAsync(roomId);
                if (videoCall == null)
                {
                    return null;
                }

                var chatMessage = new ChatMessage
                {
                    VideoCallId = videoCall.Id,
                    SenderName = senderName,
                    SenderType = senderType,
                    Message = message,
                    MessageType = messageType,
                    SentAt = DateTime.Now
                };

                _context.ChatMessages.Add(chatMessage);
                await _context.SaveChangesAsync();

                return chatMessage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving chat message: {ex.Message}");
                throw;
            }
        }

        public async Task<FileShare> SaveFileShareAsync(string roomId, string senderName, string senderType, string fileName, byte[] fileData, string fileType = null, string mimeType = null)
        {
            try
            {
                var videoCall = await GetActiveCallByRoomIdAsync(roomId);
                if (videoCall == null)
                {
                    return null;
                }

                // Dosyayı kaydet
                var uploadsDir = HostingEnvironment.MapPath("~/App_Data/VideoCallFiles");
                if (!Directory.Exists(uploadsDir))
                {
                    Directory.CreateDirectory(uploadsDir);
                }

                var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
                var filePath = Path.Combine(uploadsDir, uniqueFileName);
                
                File.WriteAllBytes(filePath, fileData);

                // File hash hesapla
                string fileHash = ComputeFileHash(fileData);

                var fileShare = new FileShare
                {
                    VideoCallId = videoCall.Id,
                    SenderName = senderName,
                    SenderType = senderType,
                    FileName = uniqueFileName,
                    OriginalFileName = fileName,
                    FilePath = filePath,
                    FileSize = fileData.Length,
                    FileType = fileType ?? GetFileType(fileName),
                    MimeType = mimeType,
                    FileHash = fileHash,
                    SharedAt = DateTime.Now,
                    ExpiresAt = DateTime.Now.AddDays(30) // 30 gün sonra silinecek
                };

                _context.FileShares.Add(fileShare);
                await _context.SaveChangesAsync();

                return fileShare;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving file share: {ex.Message}");
                throw;
            }
        }

        public async Task<CallParticipant> AddParticipantAsync(string roomId, string participantName, string participantType, int? participantId, string connectionId, string ipAddress = null, string userAgent = null)
        {
            try
            {
                var videoCall = await GetActiveCallByRoomIdAsync(roomId);
                if (videoCall == null)
                {
                    return null;
                }

                // Aynı connection ID ile katılımcı var mı kontrol et
                var existingParticipant = await _context.CallParticipants
                    .FirstOrDefaultAsync(x => x.VideoCallId == videoCall.Id && x.ConnectionId == connectionId && x.IsActive);

                if (existingParticipant != null)
                {
                    return existingParticipant;
                }

                var participant = new CallParticipant
                {
                    VideoCallId = videoCall.Id,
                    ParticipantName = participantName,
                    ParticipantType = participantType,
                    ParticipantId = participantId,
                    ConnectionId = connectionId,
                    JoinedAt = DateTime.Now,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    DeviceType = GetDeviceType(userAgent),
                    BrowserName = GetBrowserName(userAgent),
                    IsActive = true
                };

                _context.CallParticipants.Add(participant);

                // Katılımcı sayısını güncelle
                var participantCount = await _context.CallParticipants
                    .CountAsync(x => x.VideoCallId == videoCall.Id && x.IsActive);

                videoCall.ParticipantCount = participantCount + 1;
                videoCall.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                // Event kaydı
                await LogEventAsync(roomId, "user_joined", $"{participantName} joined the call", participantName);

                return participant;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding participant: {ex.Message}");
                throw;
            }
        }

        public async Task RemoveParticipantAsync(string roomId, string connectionId)
        {
            try
            {
                var participant = await _context.CallParticipants
                    .Include(x => x.VideoCall)
                    .FirstOrDefaultAsync(x => x.VideoCall.RoomId == roomId && x.ConnectionId == connectionId && x.IsActive);

                if (participant == null)
                {
                    return;
                }

                participant.LeftAt = DateTime.Now;
                participant.Duration = (int)(DateTime.Now - participant.JoinedAt).TotalSeconds;
                participant.IsActive = false;

                // Katılımcı sayısını güncelle
                var videoCall = participant.VideoCall;
                var activeParticipantCount = await _context.CallParticipants
                    .CountAsync(x => x.VideoCallId == videoCall.Id && x.IsActive && x.Id != participant.Id);

                videoCall.ParticipantCount = activeParticipantCount;
                videoCall.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                // Event kaydı
                await LogEventAsync(roomId, "user_left", $"{participant.ParticipantName} left the call", participant.ParticipantName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error removing participant: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateConnectionQualityAsync(string roomId, string quality)
        {
            try
            {
                var videoCall = await GetActiveCallByRoomIdAsync(roomId);
                if (videoCall == null) return;

                videoCall.ConnectionQuality = quality;
                videoCall.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                // Event kaydı
                await LogEventAsync(roomId, "quality_change", $"Connection quality changed to {quality}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating connection quality: {ex.Message}");
                throw;
            }
        }

        public async Task LogEventAsync(string roomId, string eventType, string description, string participantName = null, object eventData = null)
        {
            try
            {
                var videoCall = await GetActiveCallByRoomIdAsync(roomId);
                if (videoCall == null) return;

                var callEvent = new CallEvent
                {
                    VideoCallId = videoCall.Id,
                    EventType = eventType,
                    EventDescription = description,
                    ParticipantName = participantName,
                    EventData = eventData != null ? JsonConvert.SerializeObject(eventData) : null,
                    OccurredAt = DateTime.Now,
                    Severity = GetEventSeverity(eventType)
                };

                _context.CallEvents.Add(callEvent);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error logging event: {ex.Message}");
            }
        }

        public async Task<VideoCall> GetActiveCallByRoomIdAsync(string roomId)
        {
            return await _context.VideoCalls
                .FirstOrDefaultAsync(x => x.RoomId == roomId && x.Status == "active");
        }

        public async Task<List<ChatMessage>> GetChatHistoryAsync(string roomId, int pageSize = 50, int pageNumber = 1)
        {
            var videoCall = await GetActiveCallByRoomIdAsync(roomId);
            if (videoCall == null) return new List<ChatMessage>();

            return await _context.ChatMessages
                .Where(x => x.VideoCallId == videoCall.Id && !x.IsDeleted)
                .OrderBy(x => x.SentAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<List<FileShare>> GetFileSharesAsync(string roomId)
        {
            var videoCall = await GetActiveCallByRoomIdAsync(roomId);
            if (videoCall == null) return new List<FileShare>();

            return await _context.FileShares
                .Where(x => x.VideoCallId == videoCall.Id && !x.IsDeleted)
                .OrderByDescending(x => x.SharedAt)
                .ToListAsync();
        }

        // Helper Methods
        private string ComputeFileHash(byte[] fileData)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(fileData);
                return Convert.ToBase64String(hash);
            }
        }

        private string GetFileType(string fileName)
        {
            var extension = Path.GetExtension(fileName)?.ToLower();
            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".gif":
                case ".bmp":
                    return "image";
                case ".pdf":
                case ".doc":
                case ".docx":
                case ".txt":
                    return "document";
                case ".mp4":
                case ".avi":
                case ".mov":
                    return "video";
                case ".mp3":
                case ".wav":
                    return "audio";
                default:
                    return "other";
            }
        }

        private string GetDeviceType(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return "unknown";
            
            userAgent = userAgent.ToLower();
            if (userAgent.Contains("mobile") || userAgent.Contains("android") || userAgent.Contains("iphone"))
                return "mobile";
            if (userAgent.Contains("tablet") || userAgent.Contains("ipad"))
                return "tablet";
            return "desktop";
        }

        private string GetBrowserName(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return "unknown";
            
            userAgent = userAgent.ToLower();
            if (userAgent.Contains("chrome")) return "Chrome";
            if (userAgent.Contains("firefox")) return "Firefox";
            if (userAgent.Contains("safari")) return "Safari";
            if (userAgent.Contains("edge")) return "Edge";
            if (userAgent.Contains("opera")) return "Opera";
            return "other";
        }

        private string GetEventSeverity(string eventType)
        {
            switch (eventType.ToLower())
            {
                case "call_started":
                case "call_ended":
                case "user_joined":
                case "user_left":
                    return "info";
                case "quality_change":
                    return "warning";
                case "error":
                case "connection_failed":
                    return "error";
                default:
                    return "info";
            }
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}
