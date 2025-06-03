using Microsoft.AspNet.SignalR;
using System;
using System.Threading.Tasks;
using VideoCallProject.Services;

namespace VideoCallProject.Hubs
{
    public class VideoCallHub : Hub
    {
        private readonly IVideoCallService _videoCallService;

        public VideoCallHub()
        {
            _videoCallService = new VideoCallService();
        }

        public async Task JoinRoom(string roomId)
        {
            await Groups.Add(Context.ConnectionId, roomId);
            
            // Katılımcıyı veritabanına ekle
            var participantName = Context.QueryString["participantName"] ?? "Unknown";
            var participantType = Context.QueryString["participantType"] ?? "customer";
            var participantId = Context.QueryString["participantId"];
            var ipAddress = GetClientIpAddress();
            var userAgent = Context.Request.Headers["User-Agent"];

            int? parsedParticipantId = null;
            if (int.TryParse(participantId, out int id))
            {
                parsedParticipantId = id;
            }

            try
            {
                await _videoCallService.AddParticipantAsync(roomId, participantName, participantType, 
                    parsedParticipantId, Context.ConnectionId, ipAddress, userAgent);

                // Diğer kullanıcılara bildir
                Clients.OthersInGroup(roomId).userJoined(participantName, Context.QueryString["konu"]);
            }
            catch (Exception ex)
            {
                // Hata durumunda client'a bildir
                Clients.Caller.joinError($"Odaya katılım hatası: {ex.Message}");
            }
        }

        public async Task Send(string roomId, string message)
        {
            // WebRTC signaling mesajlarını ilet
            Clients.Group(roomId).receiveMessage(message);
        }

        public async Task SendChat(string roomId, string senderName, string message)
        {
            try
            {
                // Chat mesajını veritabanına kaydet
                var chatMessage = await _videoCallService.SaveChatMessageAsync(roomId, senderName, "customer", message);
                
                if (chatMessage != null)
                {
                    // Odadaki herkese gönder
                    Clients.Group(roomId).receiveChat(senderName, message);
                }
                else
                {
                    Clients.Caller.chatError("Mesaj kaydedilemedi");
                }
            }
            catch (Exception ex)
            {
                Clients.Caller.chatError($"Chat hatası: {ex.Message}");
            }
        }

        public async Task SendFile(string roomId, string senderName, string fileName, byte[] fileBytes)
        {
            try
            {
                // Dosya boyutu kontrolü (50MB)
                if (fileBytes.Length > 50 * 1024 * 1024)
                {
                    Clients.Caller.fileError("Dosya çok büyük (Max 50MB)");
                    return;
                }

                // Dosyayı veritabanına kaydet
                var fileShare = await _videoCallService.SaveFileShareAsync(roomId, senderName, "customer", fileName, fileBytes);
                
                if (fileShare != null)
                {
                    // Odadaki herkese dosyayı gönder
                    Clients.Group(roomId).receiveFile(senderName, fileName, fileBytes);
                }
                else
                {
                    Clients.Caller.fileError("Dosya kaydedilemedi");
                }
            }
            catch (Exception ex)
            {
                Clients.Caller.fileError($"Dosya gönderme hatası: {ex.Message}");
            }
        }

        public async Task SendFileChunk(string roomId, string fileId, int chunkIndex, int totalChunks, byte[] chunkData, string fileName, string senderName)
        {
            try
            {
                // Chunk'ı odadaki herkese ilet
                Clients.Group(roomId).receiveFileChunk(fileId, chunkIndex, totalChunks, chunkData, fileName, senderName);
                
                // Son chunk ise veritabanına kaydet
                if (chunkIndex == totalChunks - 1)
                {
                    await _videoCallService.LogEventAsync(roomId, "file_chunk_completed", 
                        $"Dosya chunk'ları tamamlandı: {fileName}", senderName);
                }
            }
            catch (Exception ex)
            {
                Clients.Caller.fileError($"Chunk gönderme hatası: {ex.Message}");
            }
        }

        public async Task AdSoyadGetir(string roomId, string adSoyad, string konu)
        {
            // Katılımcı bilgilerini diğerlerine gönder
            Clients.OthersInGroup(roomId).adSoyad(adSoyad, konu);
        }

        public async Task UpdateConnectionQuality(string roomId, string quality)
        {
            try
            {
                await _videoCallService.UpdateConnectionQualityAsync(roomId, quality);
            }
            catch (Exception ex)
            {
                // Sessizce log'la, client'a hata gönderme
                System.Diagnostics.Debug.WriteLine($"Connection quality update error: {ex.Message}");
            }
        }

        public async Task LogEvent(string roomId, string eventType, string description, string participantName = null)
        {
            try
            {
                await _videoCallService.LogEventAsync(roomId, eventType, description, participantName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Event logging error: {ex.Message}");
            }
        }

        public override async Task OnConnected()
        {
            var roomId = Context.QueryString["roomId"];
            var participantName = Context.QueryString["participantName"];
            
            if (!string.IsNullOrEmpty(roomId) && !string.IsNullOrEmpty(participantName))
            {
                await _videoCallService.LogEventAsync(roomId, "user_connected", 
                    $"{participantName} connected to SignalR", participantName);
            }
            
            await base.OnConnected();
        }

        public override async Task OnDisconnected(bool stopCalled)
        {
            var roomId = Context.QueryString["roomId"];
            var participantName = Context.QueryString["participantName"];
            
            if (!string.IsNullOrEmpty(roomId))
            {
                try
                {
                    // Katılımcıyı çıkar
                    await _videoCallService.RemoveParticipantAsync(roomId, Context.ConnectionId);
                    
                    // Diğer kullanıcılara bildir
                    Clients.OthersInGroup(roomId).userLeft();
                    
                    // Event log
                    if (!string.IsNullOrEmpty(participantName))
                    {
                        await _videoCallService.LogEventAsync(roomId, "user_disconnected", 
                            $"{participantName} disconnected from SignalR", participantName);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Disconnect error: {ex.Message}");
                }
            }

            await base.OnDisconnected(stopCalled);
        }

        public override async Task OnReconnected()
        {
            var roomId = Context.QueryString["roomId"];
            var participantName = Context.QueryString["participantName"];
            
            if (!string.IsNullOrEmpty(roomId))
            {
                // Tekrar gruba ekle
                await Groups.Add(Context.ConnectionId, roomId);
                
                if (!string.IsNullOrEmpty(participantName))
                {
                    await _videoCallService.LogEventAsync(roomId, "user_reconnected", 
                        $"{participantName} reconnected to SignalR", participantName);
                }
            }
            
            await base.OnReconnected();
        }

        // Helper Methods
        private string GetClientIpAddress()
        {
            try
            {
                var request = Context.Request;
                
                // Check for forwarded IP first (behind proxy/load balancer)
                var forwardedFor = request.Headers["X-Forwarded-For"];
                if (!string.IsNullOrEmpty(forwardedFor))
                {
                    var ips = forwardedFor.Split(',');
                    if (ips.Length > 0)
                    {
                        return ips[0].Trim();
                    }
                }
                
                // Check for real IP header
                var realIp = request.Headers["X-Real-IP"];
                if (!string.IsNullOrEmpty(realIp))
                {
                    return realIp.Trim();
                }
                
                // Fall back to remote IP
                var remoteAddr = request.Environment["server.RemoteIpAddress"]?.ToString();
                return remoteAddr ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _videoCallService?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
