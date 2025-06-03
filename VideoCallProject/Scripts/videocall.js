// Handle ICE candidates
        this.peerConnection.onicecandidate = (event) => {
            if (event.candidate) {
                this.sendSignalingMessage({
                    type: 'ice-candidate',
                    candidate: event.candidate
                });
            }
        };

        // Handle connection state changes
        this.peerConnection.onconnectionstatechange = () => {
            console.log('Connection state:', this.peerConnection.connectionState);
            
            switch (this.peerConnection.connectionState) {
                case 'connected':
                    this.updateStatus('Bağlandı');
                    this.updateConnectionQuality('good');
                    this.startQualityMonitoring();
                    this.logEvent('webrtc_connected', 'WebRTC connection established');
                    break;
                case 'connecting':
                    this.updateStatus('Bağlanıyor...');
                    break;
                case 'disconnected':
                    this.updateStatus('Bağlantı kesildi');
                    this.updateConnectionQuality('poor');
                    this.logEvent('webrtc_disconnected', 'WebRTC connection lost');
                    break;
                case 'failed':
                    this.updateStatus('Bağlantı başarısız');
                    this.updateConnectionQuality('poor');
                    this.logEvent('webrtc_failed', 'WebRTC connection failed');
                    break;
            }
        };

        // Handle ICE connection state changes
        this.peerConnection.oniceconnectionstatechange = () => {
            console.log('ICE connection state:', this.peerConnection.iceConnectionState);
            this.logEvent('ice_state_change', `ICE state: ${this.peerConnection.iceConnectionState}`);
        };

    } catch (error) {
        console.error('PeerConnection creation error:', error);
        this.updateStatus('Bağlantı hatası');
        this.logEvent('peer_connection_error', error.message);
    }
}

// Initiate call (create offer)
async doCall() {
    try {
        console.log('Creating offer...');
        this.updateStatus('Offer oluşturuluyor...');

        const offer = await this.peerConnection.createOffer({
            offerToReceiveAudio: true,
            offerToReceiveVideo: true
        });

        await this.peerConnection.setLocalDescription(offer);
        this.sendSignalingMessage({ type: 'offer', offer: offer });
        this.updateStatus('Bağlantı teklifi gönderildi...');
        this.logEvent('offer_sent', 'WebRTC offer sent');

    } catch (error) {
        console.error('doCall error:', error);
        this.updateStatus('Bağlantı hatası: ' + error.message);
        this.logEvent('offer_error', error.message);
    }
}

// Handle incoming offer
async handleOffer(offer) {
    try {
        console.log('Handling offer...');
        
        if (!this.peerConnection) {
            this.createPeerConnection();
        }

        await this.peerConnection.setRemoteDescription(new RTCSessionDescription(offer));
        
        const answer = await this.peerConnection.createAnswer();
        await this.peerConnection.setLocalDescription(answer);
        
        this.sendSignalingMessage({ type: 'answer', answer: answer });
        this.updateStatus('Bağlantı yanıtı gönderildi');
        this.logEvent('answer_sent', 'WebRTC answer sent');

    } catch (error) {
        console.error('handleOffer error:', error);
        this.updateStatus('Offer işleme hatası');
        this.logEvent('offer_handle_error', error.message);
    }
}

// Handle incoming answer
async handleAnswer(answer) {
    try {
        await this.peerConnection.setRemoteDescription(new RTCSessionDescription(answer));
        this.updateStatus('Bağlantı kuruldu');
        this.logEvent('answer_received', 'WebRTC answer received');
    } catch (error) {
        console.error('handleAnswer error:', error);
        this.updateStatus('Answer işleme hatası');
        this.logEvent('answer_handle_error', error.message);
    }
}

// Handle ICE candidate
async handleIceCandidate(candidate) {
    try {
        if (this.peerConnection && candidate) {
            await this.peerConnection.addIceCandidate(new RTCIceCandidate(candidate));
        }
    } catch (error) {
        console.error('ICE candidate error:', error);
        this.logEvent('ice_candidate_error', error.message);
    }
}

// Handle remote hangup
handleRemoteHangup() {
    if (this.remoteStream) {
        this.remoteStream.getTracks().forEach(track => track.stop());
    }

    this.remoteVideo.srcObject = null;
    this.remotePlaceholder.style.display = 'block';

    if (this.peerConnection) {
        this.peerConnection.close();
        this.peerConnection = null;
    }

    this.isStarted = false;
    this.isChannelReady = false;
    this.updateStatus('Karşı taraf ayrıldı');
    this.logEvent('remote_hangup', 'Remote participant left');
}

// Start quality monitoring
startQualityMonitoring() {
    if (!this.peerConnection) return;

    setInterval(async () => {
        try {
            const stats = await this.peerConnection.getStats();
            this.analyzeConnectionStats(stats);
        } catch (error) {
            console.error('Stats monitoring error:', error);
        }
    }, 5000);
}

// Analyze connection statistics
analyzeConnectionStats(stats) {
    let packetsLost = 0;
    let packetsReceived = 0;
    let bytesReceived = 0;
    let currentRoundTripTime = 0;

    stats.forEach(report => {
        if (report.type === 'inbound-rtp' && report.mediaType === 'video') {
            packetsLost = report.packetsLost || 0;
            packetsReceived = report.packetsReceived || 0;
            bytesReceived = report.bytesReceived || 0;
        }
        
        if (report.type === 'candidate-pair' && report.state === 'succeeded') {
            currentRoundTripTime = report.currentRoundTripTime || 0;
        }
    });

    // Update stats
    this.connectionStats = {
        packetsLost,
        packetsReceived,
        bytesReceived,
        currentRoundTripTime: currentRoundTripTime * 1000 // Convert to ms
    };

    // Determine quality
    let quality = 'good';
    if (packetsLost > 100 || currentRoundTripTime > 0.3) {
        quality = 'poor';
    } else if (packetsLost > 50 || currentRoundTripTime > 0.15) {
        quality = 'medium';
    }

    this.updateConnectionQuality(quality);
    this.updateConnectionQualityDB(quality);
}

// Send signaling message
sendSignalingMessage(message) {
    if (this.connection.state === $.signalR.connectionState.connected) {
        this.hub.invoke('send', this.config.roomId, JSON.stringify(message))
            .fail(error => console.error('Signaling message send error:', error));
    }
}

// Chat functionality
async sendChatMessage() {
    const input = document.getElementById('chatInput');
    const message = input.value.trim();
    
    if (message !== "") {
        try {
            await this.hub.invoke("sendChat", this.config.roomId, this.config.adSoyad, message);
            this.displayChatMessage(this.config.adSoyad, message, true);
            input.value = '';
        } catch (error) {
            console.error('Chat send error:', error);
            this.showNotification('Mesaj gönderilemedi', 'error');
        }
    }
}

// Display chat message
displayChatMessage(sender, message, isSent = false) {
    const chatBox = document.getElementById("chatMessages");
    const safeMessage = message.replace(/</g, "&lt;").replace(/>/g, "&gt;");
    
    const alignmentClass = isSent ? 'chat-right' : 'chat-left';
    const displayName = isSent ? 'Sen' : sender;

    const msgDiv = document.createElement("div");
    msgDiv.className = `chat-message ${alignmentClass}`;
    msgDiv.innerHTML = `
        <div class="message-content">
            <strong>${displayName}:</strong> ${safeMessage}
            <small class="message-time">${new Date().toLocaleTimeString()}</small>
        </div>
    `;
    
    chatBox.appendChild(msgDiv);
    chatBox.scrollTop = chatBox.scrollHeight;
}

// File upload handling
async handleFileUpload() {
    const fileInput = document.getElementById('fileInput');
    const files = fileInput.files;

    if (!files || files.length === 0) {
        this.showNotification("Lütfen bir dosya seçin.", 'warning');
        return;
    }

    if (this.connection.state !== $.signalR.connectionState.connected) {
        this.showNotification("SignalR bağlantısı yok. Lütfen bekleyin.", 'error');
        return;
    }

    for (let i = 0; i < files.length; i++) {
        const file = files[i];

        if (file.size > 50 * 1024 * 1024) {
            this.showNotification(`Dosya çok büyük: ${file.name} (Max 50MB)`, 'error');
            continue;
        }

        try {
            await this.sendFileWithDatabase(file);
        } catch (error) {
            console.error("File send error:", error);
            this.showNotification(`Hata: ${error.message}`, 'error');
        }
    }

    fileInput.value = '';
}

// Send file with database storage
async sendFileWithDatabase(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();

        reader.onerror = function (error) {
            console.error("File read error:", error);
            reject(error);
        };

        reader.onload = async function (e) {
            try {
                const byteArray = Array.from(new Uint8Array(e.target.result));

                console.log(`Sending file: ${file.name} (${this.formatFileSize(byteArray.length)})`);
                this.showNotification(`Dosya gönderiliyor: ${file.name}`, 'info');

                await this.hub.invoke("sendFile", this.config.roomId, this.config.adSoyad, file.name, byteArray);

                console.log("File sent successfully:", file.name);
                this.showNotification(`Dosya başarıyla gönderildi: ${file.name}`, 'success');
                
                await this.logEvent('file_sent', `File sent: ${file.name} (${this.formatFileSize(file.size)})`);
                
                resolve(true);

            } catch (err) {
                console.error("File send error:", err);
                this.showNotification(`Dosya gönderme hatası: ${err.message}`, 'error');
                reject(err);
            }
        }.bind(this);

        reader.readAsArrayBuffer(file);
    });
}

// Display received file
displayReceivedFile(sender, fileName, fileBytes) {
    try {
        const chatBox = document.getElementById("chatMessages");
        
        const bytes = this.base64ToBytes(fileBytes);
        const blob = new Blob([bytes]);
        const url = URL.createObjectURL(blob);

        const msgDiv = document.createElement("div");
        msgDiv.className = `chat-message chat-left file-message`;
        msgDiv.innerHTML = `
            <div class="file-content">
                <strong>${sender}:</strong><br>
                📎 <a href="${url}" download="${fileName}" class="file-download-link">
                    ${fileName}
                </a>
                <br><small class="file-size">${this.formatFileSize(bytes.length)}</small>
                <br><small class="message-time">${new Date().toLocaleTimeString()}</small>
            </div>
        `;

        chatBox.appendChild(msgDiv);
        chatBox.scrollTop = chatBox.scrollHeight;

        setTimeout(() => URL.revokeObjectURL(url), 300000);

    } catch (error) {
        console.error('File display error:', error);
        this.showNotification('Dosya görüntüleme hatası', 'error');
    }
}

// Handle file chunks
handleFileChunk(fileId, chunkIndex, totalChunks, chunkData, fileName, sender) {
    if (!window.fileChunks) window.fileChunks = {};
    
    if (!window.fileChunks[fileId]) {
        window.fileChunks[fileId] = {
            chunks: new Array(totalChunks),
            fileName: fileName,
            sender: sender,
            receivedChunks: 0
        };
    }

    window.fileChunks[fileId].chunks[chunkIndex] = new Uint8Array(chunkData);
    window.fileChunks[fileId].receivedChunks++;

    const progress = Math.round((window.fileChunks[fileId].receivedChunks / totalChunks) * 100);
    this.showNotification(`Dosya alınıyor: ${fileName} - %${progress}`, 'info');

    if (window.fileChunks[fileId].receivedChunks === totalChunks) {
        console.log(`All chunks received: ${fileName}`);

        const totalSize = window.fileChunks[fileId].chunks.reduce((sum, chunk) => sum + chunk.length, 0);
        const completeFile = new Uint8Array(totalSize);
        let offset = 0;

        for (const chunk of window.fileChunks[fileId].chunks) {
            completeFile.set(chunk, offset);
            offset += chunk.length;
        }

        this.displayReceivedFile(window.fileChunks[fileId].sender, fileName, btoa(String.fromCharCode(...completeFile)));
        delete window.fileChunks[fileId];
    }
}

// Control functions
toggleMicrophone() {
    if (this.localStream) {
        const audioTrack = this.localStream.getAudioTracks()[0];
        if (audioTrack) {
            audioTrack.enabled = !audioTrack.enabled;
            const micBtn = document.getElementById('micBtn');
            const icon = micBtn.querySelector('i');

            if (audioTrack.enabled) {
                micBtn.className = 'control-btn active';
                icon.className = 'fas fa-microphone';
                this.logEvent('microphone_enabled', 'Microphone enabled');
            } else {
                micBtn.className = 'control-btn inactive';
                icon.className = 'fas fa-microphone-slash';
                this.logEvent('microphone_disabled', 'Microphone disabled');
            }
        }
    }
}

toggleCamera() {
    if (this.localStream) {
        const videoTrack = this.localStream.getVideoTracks()[0];
        if (videoTrack) {
            videoTrack.enabled = !videoTrack.enabled;
            const cameraBtn = document.getElementById('cameraBtn');
            const icon = cameraBtn.querySelector('i');

            if (videoTrack.enabled) {
                cameraBtn.className = 'control-btn active';
                icon.className = 'fas fa-video';
                this.logEvent('camera_enabled', 'Camera enabled');
            } else {
                cameraBtn.className = 'control-btn inactive';
                icon.className = 'fas fa-video-slash';
                this.logEvent('camera_disabled', 'Camera disabled');
            }
        }
    }
}

async toggleScreenShare() {
    const screenBtn = document.getElementById('screenShareBtn');
    const icon = screenBtn.querySelector('i');

    try {
        if (screenBtn.classList.contains('active')) {
            await this.startLocalVideo();
            screenBtn.className = 'control-btn neutral';
            icon.className = 'fas fa-desktop';
            this.logEvent('screen_share_stopped', 'Screen sharing stopped');
        } else {
            const screenStream = await navigator.mediaDevices.getDisplayMedia({
                video: true,
                audio: true
            });

            this.localVideo.srcObject = screenStream;
            screenBtn.className = 'control-btn active';
            icon.className = 'fas fa-stop';
            this.logEvent('screen_share_started', 'Screen sharing started');

            screenStream.getVideoTracks()[0].onended = () => {
                this.startLocalVideo();
                screenBtn.className = 'control-btn neutral';
                icon.className = 'fas fa-desktop';
                this.logEvent('screen_share_ended', 'Screen sharing ended');
            };

            if (this.peerConnection) {
                const sender = this.peerConnection.getSenders().find(s =>
                    s.track && s.track.kind === 'video'
                );
                if (sender) {
                    await sender.replaceTrack(screenStream.getVideoTracks()[0]);
                }
            }
        }
    } catch (error) {
        console.error('Screen share error:', error);
        this.logEvent('screen_share_error', error.message);
    }
}

// End call
endCall() {
    this.logEvent('call_ending', 'User ending call');

    if (this.localStream) {
        this.localStream.getTracks().forEach(track => track.stop());
    }

    if (this.remoteStream) {
        this.remoteStream.getTracks().forEach(track => track.stop());
    }

    if (this.peerConnection) {
        this.peerConnection.close();
    }

    this.sendSignalingMessage({ type: 'bye' });

    fetch('/VideoCall/KullaniciAyrildi', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            connectionId: this.config.talepId,
            isBusy: false
        })
    }).then(res => res.json())
    .then(data => {
        if (data.success) {
            if (this.config.tip == "server") {
                window.location.href = "/VideoCall/Index";
            } else {
                window.location.href = "/VideoCall/Degerlendirme";
            }
        }
    });
}

// Utility functions
updateStatus(status) {
    this.connectionStatus.textContent = status;
    console.log('Status:', status);
}

updateParticipantCount() {
    this.participantCountElement.innerHTML = `<i class="fas fa-users"></i> ${this.participantCount} kişi`;
}

updateConnectionQuality(quality) {
    const qualityMap = {
        'good': { text: 'İyi', class: 'quality-good', icon: 'fas fa-signal' },
        'medium': { text: 'Orta', class: 'quality-medium', icon: 'fas fa-signal' },
        'poor': { text: 'Kötü', class: 'quality-poor', icon: 'fas fa-signal' }
    };

    const q = qualityMap[quality] || qualityMap['medium'];
    this.connectionQuality.innerHTML = `<i class="${q.icon}"></i> ${q.text}`;
    this.connectionQuality.className = `connection-quality ${q.class}`;
}

async updateConnectionQualityDB(quality) {
    try {
        await fetch('/VideoCall/UpdateConnectionQuality', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                roomId: this.config.roomId,
                quality: quality
            })
        });
    } catch (error) {
        console.error('Connection quality update error:', error);
    }
}

async logEvent(eventType, description, participantName = null) {
    try {
        await fetch('/VideoCall/LogEvent', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                roomId: this.config.roomId,
                eventType: eventType,
                description: description,
                participantName: participantName || this.config.adSoyad
            })
        });
    } catch (error) {
        console.error('Event log error:', error);
    }
}

// Load data functions
async loadChatHistory() {
    try {
        const response = await fetch(`/VideoCall/GetChatHistory?roomId=${this.config.roomId}&page=1&pageSize=50`);
        const result = await response.json();
        
        if (result.success) {
            const chatBox = document.getElementById("chatMessages");
            chatBox.innerHTML = '';
            
            result.data.forEach(message => {
                const msgDiv = document.createElement("div");
                const alignmentClass = message.senderType === 'operator' ? 'chat-left' : 'chat-right';
                
                msgDiv.className = `chat-message ${alignmentClass}`;
                msgDiv.innerHTML = `
                    <div class="message-content">
                        <strong>${message.senderName}:</strong> ${message.message}
                        <small class="message-time">${message.sentAt}</small>
                    </div>
                `;
                chatBox.appendChild(msgDiv);
            });
            
            chatBox.scrollTop = chatBox.scrollHeight;
        }
    } catch (error) {
        console.error('Chat history load error:', error);
    }
}

async loadFileShareHistory() {
    try {
        const response = await fetch(`/VideoCall/GetFileShares?roomId=${this.config.roomId}`);
        const result = await response.json();
        
        if (result.success && result.data.length > 0) {
            const chatBox = document.getElementById("chatMessages");
            
            result.data.forEach(file => {
                const msgDiv = document.createElement("div");
                const alignmentClass = file.senderType === 'operator' ? 'chat-left' : 'chat-right';
                
                msgDiv.className = `chat-message ${alignmentClass} file-message`;
                msgDiv.innerHTML = `
                    <div class="file-content">
                        <strong>${file.senderName}:</strong><br>
                        📎 <a href="${file.downloadUrl}" target="_blank" class="file-download-link">
                            ${file.fileName}
                        </a>
                        <br><small class="file-size">${this.formatFileSize(file.fileSize)}</small>
                        <br><small class="message-time">${file.sharedAt}</small>
                    </div>
                `;
                chatBox.appendChild(msgDiv);
            });
            
            chatBox.scrollTop = chatBox.scrollHeight;
        }
    } catch (error) {
        console.error('File history load error:', error);
    }
}

async loadCallStats() {
    try {
        const response = await fetch(`/VideoCall/GetCallStats?roomId=${this.config.roomId}`);
        const result = await response.json();
        
        if (result.success) {
            const stats = result.data;
            console.log('Call statistics:', stats);
            
            const statsDiv = document.getElementById('callStats');
            if (statsDiv) {
                statsDiv.innerHTML = `
                    <div class="stats-item">Başlangıç: ${stats.startTime}</div>
                    <div class="stats-item">Mesaj: ${stats.messageCount}</div>
                    <div class="stats-item">Dosya: ${stats.fileCount}</div>
                    <div class="stats-item">Katılımcı: ${stats.participantCount}</div>
                `;
            }
        }
    } catch (error) {
        console.error('Stats load error:', error);
    }
}

// Event listeners setup
setupEventListeners() {
    window.addEventListener('load', () => this.initialize());
    
    document.addEventListener('DOMContentLoaded', () => {
        // Control buttons
        document.getElementById('micBtn')?.addEventListener('click', () => this.toggleMicrophone());
        document.getElementById('cameraBtn')?.addEventListener('click', () => this.toggleCamera());
        document.getElementById('screenShareBtn')?.addEventListener('click', () => this.toggleScreenShare());
        document.getElementById('endCallBtn')?.addEventListener('click', () => this.endCall());
        
        // Chat
        document.getElementById('sendChatBtn')?.addEventListener('click', () => this.sendChatMessage());
        document.getElementById('chatInput')?.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') {
                this.sendChatMessage();
            }
        });
        
        // File upload
        document.getElementById('fileInput')?.addEventListener('change', () => this.handleFileUpload());
    });

    window.addEventListener('beforeunload', () => {
        this.logEvent('page_unload', 'Page unloading');
        
        if (this.localStream) {
            this.localStream.getTracks().forEach(track => track.stop());
        }
        if (this.peerConnection) {
            this.peerConnection.close();
        }
        this.sendSignalingMessage({ type: 'bye' });
    });
}

// Helper functions
showNotification(message, type = 'info') {
    const notification = document.createElement('div');
    notification.className = `notification ${type}`;
    notification.textContent = message;
    document.body.appendChild(notification);

    setTimeout(() => {
        if (notification.parentNode) {
            notification.remove();
        }
    }, 4000);
}

formatFileSize(bytes) {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

base64ToBytes(base64) {
    const binaryString = atob(base64);
    const bytes = new Uint8Array(binaryString.length);
    for (let i = 0; i < binaryString.length; i++) {
        bytes[i] = binaryString.charCodeAt(i);
    }
    return bytes;
}
