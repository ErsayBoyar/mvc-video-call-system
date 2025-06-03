-- =====================================================
-- MSSQL VERİTABANI ŞEMASI - GÖRÜNTÜLÜ GÖRÜŞME VE CHAT
-- =====================================================

-- 1. Ana VideoCall Tablosu
CREATE TABLE VideoCalls (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    RoomId NVARCHAR(100) NOT NULL,
    TalepId INT NOT NULL,
    OperatorId INT NULL,
    CallerName NVARCHAR(200) NULL,
    CallerType NVARCHAR(50) NULL, -- 'operator', 'customer'
    Subject NVARCHAR(500) NULL,
    StartTime DATETIME2 NOT NULL DEFAULT GETDATE(),
    EndTime DATETIME2 NULL,
    Duration INT NOT NULL DEFAULT 0, -- saniye cinsinden
    Status NVARCHAR(50) NOT NULL DEFAULT 'active', -- 'active', 'ended', 'failed', 'cancelled'
    ConnectionQuality NVARCHAR(50) NULL DEFAULT 'good', -- 'good', 'medium', 'poor'
    IsRecorded BIT NOT NULL DEFAULT 0,
    RecordingPath NVARCHAR(1000) NULL,
    RecordingSize BIGINT NULL, -- byte cinsinden
    ParticipantCount INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    UpdatedAt DATETIME2 NULL,
    
    -- İndeksler
    INDEX IX_VideoCalls_RoomId (RoomId),
    INDEX IX_VideoCalls_TalepId (TalepId),
    INDEX IX_VideoCalls_OperatorId (OperatorId),
    INDEX IX_VideoCalls_StartTime (StartTime),
    INDEX IX_VideoCalls_Status (Status)
);

-- 2. Chat Mesajları Tablosu
CREATE TABLE ChatMessages (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    VideoCallId INT NOT NULL,
    SenderName NVARCHAR(200) NOT NULL,
    SenderType NVARCHAR(50) NOT NULL, -- 'operator', 'customer'
    Message NVARCHAR(MAX) NOT NULL,
    MessageType NVARCHAR(50) NOT NULL DEFAULT 'text', -- 'text', 'file', 'system', 'emoji'
    SentAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    IsRead BIT NOT NULL DEFAULT 0,
    IsDeleted BIT NOT NULL DEFAULT 0,
    EditedAt DATETIME2 NULL,
    ReplyToMessageId INT NULL, -- mesaja yanıt özelliği için
    
    -- Foreign Key
    FOREIGN KEY (VideoCallId) REFERENCES VideoCalls(Id) ON DELETE CASCADE,
    FOREIGN KEY (ReplyToMessageId) REFERENCES ChatMessages(Id),
    
    -- İndeksler
    INDEX IX_ChatMessages_VideoCallId (VideoCallId),
    INDEX IX_ChatMessages_SentAt (SentAt),
    INDEX IX_ChatMessages_SenderType (SenderType)
);

-- 3. Dosya Paylaşımları Tablosu
CREATE TABLE FileShares (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    VideoCallId INT NOT NULL,
    SenderName NVARCHAR(200) NOT NULL,
    SenderType NVARCHAR(50) NOT NULL, -- 'operator', 'customer'
    FileName NVARCHAR(500) NOT NULL,
    OriginalFileName NVARCHAR(500) NOT NULL,
    FilePath NVARCHAR(1000) NOT NULL,
    FileSize BIGINT NOT NULL,
    FileType NVARCHAR(100) NULL, -- 'image', 'document', 'video', 'audio', 'other'
    MimeType NVARCHAR(200) NULL,
    FileHash NVARCHAR(64) NULL, -- SHA256 hash güvenlik için
    SharedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    IsDownloaded BIT NOT NULL DEFAULT 0,
    DownloadCount INT NOT NULL DEFAULT 0,
    ExpiresAt DATETIME2 NULL, -- dosya silinme tarihi
    IsDeleted BIT NOT NULL DEFAULT 0,
    
    -- Foreign Key
    FOREIGN KEY (VideoCallId) REFERENCES VideoCalls(Id) ON DELETE CASCADE,
    
    -- İndeksler
    INDEX IX_FileShares_VideoCallId (VideoCallId),
    INDEX IX_FileShares_SharedAt (SharedAt),
    INDEX IX_FileShares_FileType (FileType)
);

-- 4. Katılımcılar Tablosu
CREATE TABLE CallParticipants (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    VideoCallId INT NOT NULL,
    ParticipantName NVARCHAR(200) NOT NULL,
    ParticipantType NVARCHAR(50) NOT NULL, -- 'operator', 'customer'
    ParticipantId INT NULL, -- operator için operatorId, customer için customerId
    ConnectionId NVARCHAR(200) NULL, -- SignalR connection ID
    JoinedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    LeftAt DATETIME2 NULL,
    Duration INT NOT NULL DEFAULT 0, -- saniye cinsinden
    IpAddress NVARCHAR(50) NULL,
    UserAgent NVARCHAR(500) NULL,
    DeviceType NVARCHAR(100) NULL, -- 'desktop', 'mobile', 'tablet'
    BrowserName NVARCHAR(100) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    DisconnectReason NVARCHAR(200) NULL,
    
    -- Foreign Key
    FOREIGN KEY (VideoCallId) REFERENCES VideoCalls(Id) ON DELETE CASCADE,
    
    -- İndeksler
    INDEX IX_CallParticipants_VideoCallId (VideoCallId),
    INDEX IX_CallParticipants_ConnectionId (ConnectionId),
    INDEX IX_CallParticipants_JoinedAt (JoinedAt)
);

-- 5. Görüşme Kalitesi İstatistikleri Tablosu
CREATE TABLE CallQualityStats (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    VideoCallId INT NOT NULL,
    ParticipantId INT NULL, -- CallParticipants tablosundan
    RecordedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    PacketsLost INT NOT NULL DEFAULT 0,
    PacketsReceived INT NOT NULL DEFAULT 0,
    BytesReceived BIGINT NOT NULL DEFAULT 0,
    BytesSent BIGINT NOT NULL DEFAULT 0,
    CurrentRoundTripTime FLOAT NULL,
    AvailableOutgoingBitrate INT NULL,
    ConnectionQuality NVARCHAR(50) NULL, -- 'excellent', 'good', 'medium', 'poor'
    VideoResolution NVARCHAR(50) NULL, -- '1920x1080', '1280x720', etc.
    AudioQuality NVARCHAR(50) NULL,
    
    -- Foreign Key
    FOREIGN KEY (VideoCallId) REFERENCES VideoCalls(Id) ON DELETE CASCADE,
    FOREIGN KEY (ParticipantId) REFERENCES CallParticipants(Id),
    
    -- İndeksler
    INDEX IX_CallQualityStats_VideoCallId (VideoCallId),
    INDEX IX_CallQualityStats_RecordedAt (RecordedAt)
);

-- 6. Sistem Olayları Tablosu (Log)
CREATE TABLE CallEvents (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    VideoCallId INT NOT NULL,
    EventType NVARCHAR(100) NOT NULL, -- 'user_joined', 'user_left', 'call_started', 'call_ended', 'error', 'quality_change'
    EventDescription NVARCHAR(500) NULL,
    EventData NVARCHAR(MAX) NULL, -- JSON format
    ParticipantName NVARCHAR(200) NULL,
    OccurredAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    Severity NVARCHAR(50) NULL, -- 'info', 'warning', 'error', 'critical'
    
    -- Foreign Key
    FOREIGN KEY (VideoCallId) REFERENCES VideoCalls(Id) ON DELETE CASCADE,
    
    -- İndeksler
    INDEX IX_CallEvents_VideoCallId (VideoCallId),
    INDEX IX_CallEvents_EventType (EventType),
    INDEX IX_CallEvents_OccurredAt (OccurredAt)
);

-- =====================================================
-- STORED PROCEDURE'LER
-- =====================================================

-- 1. Yeni Görüşme Başlatma
GO
CREATE PROCEDURE sp_StartVideoCall
    @RoomId NVARCHAR(100),
    @TalepId INT,
    @OperatorId INT = NULL,
    @CallerName NVARCHAR(200),
    @CallerType NVARCHAR(50),
    @Subject NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @VideoCallId INT;
    
    -- Aynı RoomId ile aktif görüşme var mı kontrol et
    IF EXISTS (SELECT 1 FROM VideoCalls WHERE RoomId = @RoomId AND Status = 'active')
    BEGIN
        -- Mevcut görüşmeyi döndür
        SELECT * FROM VideoCalls WHERE RoomId = @RoomId AND Status = 'active';
        RETURN;
    END
    
    -- Yeni görüşme oluştur
    INSERT INTO VideoCalls (RoomId, TalepId, OperatorId, CallerName, CallerType, Subject, StartTime, Status)
    VALUES (@RoomId, @TalepId, @OperatorId, @CallerName, @CallerType, @Subject, GETDATE(), 'active');
    
    SET @VideoCallId = SCOPE_IDENTITY();
    
    -- Event kaydı
    INSERT INTO CallEvents (VideoCallId, EventType, EventDescription, ParticipantName)
    VALUES (@VideoCallId, 'call_started', 'Video call started', @CallerName);
    
    -- Oluşturulan görüşmeyi döndür
    SELECT * FROM VideoCalls WHERE Id = @VideoCallId;
END
GO

-- 2. Görüşme Sonlandırma
GO
CREATE PROCEDURE sp_EndVideoCall
    @RoomId NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @VideoCallId INT;
    DECLARE @StartTime DATETIME2;
    DECLARE @Duration INT;
    
    -- Aktif görüşmeyi bul
    SELECT @VideoCallId = Id, @StartTime = StartTime 
    FROM VideoCalls 
    WHERE RoomId = @RoomId AND Status = 'active';
    
    IF @VideoCallId IS NOT NULL
    BEGIN
        -- Duration hesapla
        SET @Duration = DATEDIFF(SECOND, @StartTime, GETDATE());
        
        -- Görüşmeyi sonlandır
        UPDATE VideoCalls 
        SET EndTime = GETDATE(), 
            Duration = @Duration, 
            Status = 'ended',
            UpdatedAt = GETDATE()
        WHERE Id = @VideoCallId;
        
        -- Aktif katılımcıları çıkar
        UPDATE CallParticipants 
        SET LeftAt = GETDATE(), 
            Duration = DATEDIFF(SECOND, JoinedAt, GETDATE()),
            IsActive = 0
        WHERE VideoCallId = @VideoCallId AND IsActive = 1;
        
        -- Event kaydı
        INSERT INTO CallEvents (VideoCallId, EventType, EventDescription)
        VALUES (@VideoCallId, 'call_ended', 'Video call ended');
        
        -- Güncellenmiş görüşmeyi döndür
        SELECT * FROM VideoCalls WHERE Id = @VideoCallId;
    END
END
GO

-- 3. Chat Mesajı Kaydetme
GO
CREATE PROCEDURE sp_SaveChatMessage
    @RoomId NVARCHAR(100),
    @SenderName NVARCHAR(200),
    @SenderType NVARCHAR(50),
    @Message NVARCHAR(MAX),
    @MessageType NVARCHAR(50) = 'text'
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @VideoCallId INT;
    DECLARE @MessageId INT;
    
    -- Aktif görüşmeyi bul
    SELECT @VideoCallId = Id FROM VideoCalls WHERE RoomId = @RoomId AND Status = 'active';
    
    IF @VideoCallId IS NOT NULL
    BEGIN
        -- Mesajı kaydet
        INSERT INTO ChatMessages (VideoCallId, SenderName, SenderType, Message, MessageType, SentAt)
        VALUES (@VideoCallId, @SenderName, @SenderType, @Message, @MessageType, GETDATE());
        
        SET @MessageId = SCOPE_IDENTITY();
        
        -- Kaydedilen mesajı döndür
        SELECT * FROM ChatMessages WHERE Id = @MessageId;
    END
END
GO

-- =====================================================
-- VERİ SORGULAMA VIEW'LERİ
-- =====================================================

-- 1. Görüşme Özeti View
GO
CREATE VIEW vw_VideoCallSummary
AS
SELECT 
    vc.Id,
    vc.RoomId,
    vc.TalepId,
    vc.OperatorId,
    vc.CallerName,
    vc.Subject,
    vc.StartTime,
    vc.EndTime,
    vc.Duration,
    vc.Status,
    vc.ConnectionQuality,
    vc.ParticipantCount,
    (SELECT COUNT(*) FROM ChatMessages cm WHERE cm.VideoCallId = vc.Id AND cm.IsDeleted = 0) as MessageCount,
    (SELECT COUNT(*) FROM FileShares fs WHERE fs.VideoCallId = vc.Id AND fs.IsDeleted = 0) as FileCount,
    (SELECT COUNT(*) FROM CallParticipants cp WHERE cp.VideoCallId = vc.Id) as TotalParticipants
FROM VideoCalls vc;
GO

-- 2. Chat Mesajları Detay View
GO
CREATE VIEW vw_ChatMessageDetails
AS
SELECT 
    cm.Id,
    cm.VideoCallId,
    vc.RoomId,
    cm.SenderName,
    cm.SenderType,
    cm.Message,
    cm.MessageType,
    cm.SentAt,
    cm.IsRead,
    cm.IsDeleted,
    vc.Subject as CallSubject
FROM ChatMessages cm
INNER JOIN VideoCalls vc ON cm.VideoCallId = vc.Id;
GO

"Commit new file" tıklayın

🎯 ADIM 2: Models Klasörünü Oluşturun
VideoCall.cs Dosyası

"Add file" → "Create new file" tıklayın
Dosya adı: VideoCallProject/Models/VideoCall.cs
İçerik:

csharpusing System;
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
