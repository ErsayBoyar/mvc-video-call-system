using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using VideoCallProject.Models;
using VideoCallProject.Services;

namespace VideoCallProject.Controllers
{
    public class VideoCallController : Controller
    {
        private readonly IVideoCallService _videoCallService;

        public VideoCallController()
        {
            _videoCallService = new VideoCallService();
        }

        // Ana görüşme sayfası
        public async Task<ActionResult> Room(string roomId, int talepId, int? operatorId, string adSoyad, string konu, string tip)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                return RedirectToAction("Index");
            }

            try
            {
                // Görüşmeyi başlat veya mevcut olanı getir
                var callerType = operatorId.HasValue ? "operator" : "customer";
                var videoCall = await _videoCallService.StartVideoCallAsync(roomId, talepId, operatorId, adSoyad, callerType, konu);

                ViewBag.RoomId = roomId;
                ViewBag.talepId = talepId;
                ViewBag.OperatorId = operatorId ?? 0;
                ViewBag.adSoyad = adSoyad;
                ViewBag.konu = konu;
                ViewBag.tip = tip;
                ViewBag.VideoCallId = videoCall.Id;

                return View();
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Görüşme başlatılırken hata oluştu: " + ex.Message;
                return View("Error");
            }
        }

        // Operatör meşgul durumu güncelleme
        [HttpPost]
        public async Task<JsonResult> SetBusy(string connectionId, bool isBusy)
        {
            try
            {
                // Burada operatör durumunu güncelleyebilirsiniz
                // Örnek: await _operatorService.SetBusyStatus(operatorId, isBusy);
                
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Kullanıcı ayrıldığında
        [HttpPost]
        public async Task<JsonResult> KullaniciAyrildi(string connectionId, bool isBusy)
        {
            try
            {
                // Görüşmeyi sonlandır
                var roomId = Request.QueryString["roomId"];
                if (!string.IsNullOrEmpty(roomId))
                {
                    await _videoCallService.EndVideoCallAsync(roomId);
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Chat geçmişini getir
        [HttpGet]
        public async Task<JsonResult> GetChatHistory(string roomId, int page = 1, int pageSize = 50)
        {
            try
            {
                var messages = await _videoCallService.GetChatHistoryAsync(roomId, pageSize, page);
                
                var result = messages.Select(m => new
                {
                    id = m.Id,
                    senderName = m.SenderName,
                    senderType = m.SenderType,
                    message = m.Message,
                    messageType = m.MessageType,
                    sentAt = m.SentAt.ToString("dd.MM.yyyy HH:mm:ss"),
                    isRead = m.IsRead
                }).ToList();

                return Json(new { success = true, data = result }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // Dosya paylaşım geçmişini getir
        [HttpGet]
        public async Task<JsonResult> GetFileShares(string roomId)
        {
            try
            {
                var files = await _videoCallService.GetFileSharesAsync(roomId);
                
                var result = files.Select(f => new
                {
                    id = f.Id,
                    senderName = f.SenderName,
                    senderType = f.SenderType,
                    fileName = f.OriginalFileName,
                    fileSize = f.FileSize,
                    fileType = f.FileType,
                    sharedAt = f.SharedAt.ToString("dd.MM.yyyy HH:mm:ss"),
                    downloadUrl = Url.Action("DownloadFile", new { id = f.Id })
                }).ToList();

                return Json(new { success = true, data = result }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // Dosya indirme
        public async Task<ActionResult> DownloadFile(int id)
        {
            try
            {
                using (var context = new ApplicationDbContext())
                {
                    var fileShare = await context.FileShares.FindAsync(id);
                    if (fileShare == null || !System.IO.File.Exists(fileShare.FilePath))
                    {
                        return HttpNotFound("Dosya bulunamadı");
                    }

                    // İndirme sayısını artır
                    fileShare.DownloadCount++;
                    fileShare.IsDownloaded = true;
                    await context.SaveChangesAsync();

                    var fileBytes = System.IO.File.ReadAllBytes(fileShare.FilePath);
                    return File(fileBytes, fileShare.MimeType ?? "application/octet-stream", fileShare.OriginalFileName);
                }
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(500, "Dosya indirme hatası: " + ex.Message);
            }
        }

        // Görüşme istatistikleri
        [HttpGet]
        public async Task<JsonResult> GetCallStats(string roomId)
        {
            try
            {
                using (var context = new ApplicationDbContext())
                {
                    var videoCall = await context.VideoCalls
                        .Include("ChatMessages")
                        .Include("FileShares")
                        .Include("Participants")
                        .FirstOrDefaultAsync(x => x.RoomId == roomId);

                    if (videoCall == null)
                    {
                        return Json(new { success = false, error = "Görüşme bulunamadı" }, JsonRequestBehavior.AllowGet);
                    }

                    var stats = new
                    {
                        callId = videoCall.Id,
                        roomId = videoCall.RoomId,
                        subject = videoCall.Subject,
                        startTime = videoCall.StartTime.ToString("dd.MM.yyyy HH:mm:ss"),
                        endTime = videoCall.EndTime?.ToString("dd.MM.yyyy HH:mm:ss"),
                        duration = videoCall.Duration,
                        status = videoCall.Status,
                        participantCount = videoCall.ParticipantCount,
                        messageCount = videoCall.ChatMessages.Count(m => !m.IsDeleted),
                        fileCount = videoCall.FileShares.Count(f => !f.IsDeleted),
                        connectionQuality = videoCall.ConnectionQuality
                    };

                    return Json(new { success = true, data = stats }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // Ana sayfa - aktif görüşmeleri listele
        public async Task<ActionResult> Index()
        {
            try
            {
                using (var context = new ApplicationDbContext())
                {
                    var activeCalls = await context.VideoCalls
                        .Where(x => x.Status == "active")
                        .OrderByDescending(x => x.StartTime)
                        .Take(20)
                        .ToListAsync();

                    return View(activeCalls);
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Görüşmeler yüklenirken hata oluştu: " + ex.Message;
                return View(new List<VideoCall>());
            }
        }

        // Geçmiş görüşmeler
        public async Task<ActionResult> History(int page = 1, int pageSize = 20)
        {
            try
            {
                using (var context = new ApplicationDbContext())
                {
                    var totalCount = await context.VideoCalls.CountAsync();
                    
                    var calls = await context.VideoCalls
                        .Include("ChatMessages")
                        .Include("FileShares")
                        .Include("Participants")
                        .OrderByDescending(x => x.StartTime)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync();

                    ViewBag.Page = page;
                    ViewBag.PageSize = pageSize;
                    ViewBag.TotalCount = totalCount;
                    ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                    return View(calls);
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Geçmiş görüşmeler yüklenirken hata oluştu: " + ex.Message;
                return View(new List<VideoCall>());
            }
        }

        // Görüşme detayları
        public async Task<ActionResult> Details(int id)
        {
            try
            {
                using (var context = new ApplicationDbContext())
                {
                    var videoCall = await context.VideoCalls
                        .Include("ChatMessages")
                        .Include("FileShares")
                        .Include("Participants")
                        .Include("Events")
                        .FirstOrDefaultAsync(x => x.Id == id);

                    if (videoCall == null)
                    {
                        return HttpNotFound("Görüşme bulunamadı");
                    }

                    return View(videoCall);
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Görüşme detayları yüklenirken hata oluştu: " + ex.Message;
                return View("Error");
            }
        }

        // Değerlendirme sayfası
        public ActionResult Degerlendirme()
        {
            return View();
        }

        // API: Bağlantı kalitesini güncelle
        [HttpPost]
        public async Task<JsonResult> UpdateConnectionQuality(string roomId, string quality)
        {
            try
            {
                await _videoCallService.UpdateConnectionQualityAsync(roomId, quality);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // API: Event log
        [HttpPost]
        public async Task<JsonResult> LogEvent(string roomId, string eventType, string description, string participantName = null)
        {
            try
            {
                await _videoCallService.LogEventAsync(roomId, eventType, description, participantName);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // API: Aktif görüşme sayısı
        [HttpGet]
        public async Task<JsonResult> GetActiveCallsCount()
        {
            try
            {
                using (var context = new ApplicationDbContext())
                {
                    var count = await context.VideoCalls.CountAsync(x => x.Status == "active");
                    return Json(new { success = true, count = count }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // API: Günlük istatistikler
        [HttpGet]
        public async Task<JsonResult> GetDailyStats(DateTime? date = null)
        {
            try
            {
                var targetDate = date ?? DateTime.Today;
                var nextDay = targetDate.AddDays(1);

                using (var context = new ApplicationDbContext())
                {
                    var stats = new
                    {
                        totalCalls = await context.VideoCalls
                            .CountAsync(x => x.StartTime >= targetDate && x.StartTime < nextDay),
                        
                        activeCalls = await context.VideoCalls
                            .CountAsync(x => x.Status == "active"),
                            
                        totalMessages = await context.ChatMessages
                            .CountAsync(x => x.SentAt >= targetDate && x.SentAt < nextDay && !x.IsDeleted),
                            
                        totalFiles = await context.FileShares
                            .CountAsync(x => x.SharedAt >= targetDate && x.SharedAt < nextDay && !x.IsDeleted),
                            
                        averageDuration = await context.VideoCalls
                            .Where(x => x.StartTime >= targetDate && x.StartTime < nextDay && x.Duration > 0)
                            .AverageAsync(x => (double?)x.Duration) ?? 0
                    };

                    return Json(new { success = true, data = stats }, JsonRequestBehavior.AllowGet);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // API: Görüşmeyi manuel sonlandır
        [HttpPost]
        public async Task<JsonResult> EndCall(string roomId)
        {
            try
            {
                var videoCall = await _videoCallService.EndVideoCallAsync(roomId);
                if (videoCall != null)
                {
                    return Json(new { success = true, message = "Görüşme sonlandırıldı" });
                }
                else
                {
                    return Json(new { success = false, error = "Aktif görüşme bulunamadı" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
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
