using System.Collections.Generic;
using System.Threading.Tasks;
using VideoCallProject.Models;

namespace VideoCallProject.Services
{
    public interface IVideoCallService
    {
        Task<VideoCall> StartVideoCallAsync(string roomId, int talepId, int? operatorId, string callerName, string callerType, string subject);
        Task<VideoCall> EndVideoCallAsync(string roomId);
        Task<ChatMessage> SaveChatMessageAsync(string roomId, string senderName, string senderType, string message, string messageType = "text");
        Task<FileShare> SaveFileShareAsync(string roomId, string senderName, string senderType, string fileName, byte[] fileData, string fileType = null, string mimeType = null);
        Task<CallParticipant> AddParticipantAsync(string roomId, string participantName, string participantType, int? participantId, string connectionId, string ipAddress = null, string userAgent = null);
        Task RemoveParticipantAsync(string roomId, string connectionId);
        Task UpdateConnectionQualityAsync(string roomId, string quality);
        Task LogEventAsync(string roomId, string eventType, string description, string participantName = null, object eventData = null);
        Task<VideoCall> GetActiveCallByRoomIdAsync(string roomId);
        Task<List<ChatMessage>> GetChatHistoryAsync(string roomId, int pageSize = 50, int pageNumber = 1);
        Task<List<FileShare>> GetFileSharesAsync(string roomId);
    }
}
