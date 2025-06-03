/**
 * VIDEO CALL JAVASCRIPT - COMPLETE WEBRTC IMPLEMENTATION
 * MVC 5.0 + SignalR + Database Integration
 */

class VideoCallManager {
    constructor(config) {
        this.config = config;
        this.localStream = null;
        this.remoteStream = null;
        this.peerConnection = null;
        this.isInitiator = false;
        this.isChannelReady = false;
        this.isStarted = false;
        this.participantCount = 1;
        this.callStartTime = new Date();
        this.connectionStats = {
            packetsLost: 0,
            bytesReceived: 0,
            bytesSent: 0,
            currentRoundTripTime: 0
        };

        // DOM elements
        this.localVideo = document.getElementById('localVideo');
        this.remoteVideo = document.getElementById('remoteVideo');
        this.remotePlaceholder = document.getElementById('remotePlaceholder');
        this.connectionStatus = document.getElementById('connectionStatus');
        this.participantCountElement = document.getElementById('participantCount');
        this.connectionQuality = document.getElementById('connectionQuality');

        // WebRTC configuration
        this.pcConfig = {
            iceServers: [
                { urls: 'stun:stun.l.google.com:19302' },
                { urls: 'stun:stun1.l.google.com:19302' },
                { urls: 'stun:stun2.l.google.com:19302' },
                { urls: 'stun:stun3.l.google.com:19302' },
                { urls: 'stun:stun4.l.google.com:19302' }
            ],
            iceCandidatePoolSize: 10
        };

        this.initializeSignalR();
        this.setupEventListeners();
    }

    // Initialize SignalR connection
    initializeSignalR() {
        this.connection = $.hubConnection("/signalr", {
            qs: { 
                talepId: this.config.talepId,
                roomId: this.config.roomId,
                participantName: this.config.adSoyad,
                participantType: this.config.operatorId > 0 ? 'operator' : 'customer',
                participantId: this.config.operatorId || null,
                konu: this.config.konu
            },
            logging: true,
            useDefaultPath: false
        });

        this.connection.disconnectTimeout = 60000;
        this.connection.keepAliveInterval = 15000;
        this.connection.transportConnectTimeout = 30000;

        this.hub = this.connection.createHubProxy('videoCallHub');
        this.setupSignalRHandlers();
    }

    // Setup SignalR event handlers
    setupSignalRHandlers() {
        // WebRTC signaling
        this.hub.on('receiveMessage', (message) => this.handleSignalingMessage(message));
        
        // User management
        this.hub.on('userJoined', (adSoyad, konu) => this.handleUserJoined(adSoyad, konu));
        this.hub.on('userLeft', () => this.handleUserLeft());
        
        // Chat and file sharing
        this.hub.on('receiveChat', (sender, message) => this.displayChatMessage(sender, message, false));
        this.hub.on('receiveFile', (sender, fileName, fileBytes) => this.displayReceivedFile(sender, fileName, fileBytes));
        this.hub.on('receiveFileChunk', (fileId, chunkIndex, totalChunks, chunkData, fileName, sender) => {
            this.handleFileChunk(fileId, chunkIndex, totalChunks, chunkData, fileName, sender);
        });

        // Connection events
        this.hub.on('joinError', (error) => this.showNotification(error, 'error'));
        this.hub.on('chatError', (error) => this.showNotification('Chat hatası: ' + error, 'error'));
        this.hub.on('fileError', (error) => this.showNotification('Dosya hatası: ' + error, 'error'));
    }

    // Initialize the video call
    async initialize() {
        try {
            this.updateStatus('Kamera açılıyor...');
            await this.startLocalVideo();
            await this.connectToSignalR();
            await this.loadChatHistory();
            await this.loadFileShareHistory();
            await this.loadCallStats();
            
            this.updateStatus('Hazır - Katılımcı bekleniyor...');
        } catch (error) {
            console.error('Initialization error:', error);
            this.updateStatus('Hata: ' + error.message);
            await this.logEvent('initialization_error', error.message);
        }
    }

    // Start local video stream
    async startLocalVideo() {
        try {
            const constraints = {
                video: { 
                    width: { ideal: 1280, max: 1920 },
                    height: { ideal: 720, max: 1080 },
                    frameRate: { ideal: 30, max: 60 }
                },
                audio: { 
                    echoCancellation: true, 
                    noiseSuppression: true,
                    autoGainControl: true,
                    sampleRate: 44100
                }
            };

            this.localStream = await navigator.mediaDevices.getUserMedia(constraints);
            this.localVideo.srcObject = this.localStream;
            
            await this.logEvent('camera_enabled', 'Local camera started successfully');
            this.updateStatus('Kamera başarıyla açıldı');
            
            return true;
        } catch (error) {
            console.error('Camera access error:', error);
            await this.logEvent('camera_error', `Camera access failed: ${error.message}`);
            throw new Error('Kamera erişimi reddedildi');
        }
    }

    // Connect to SignalR
    async connectToSignalR() {
        try {
            this.updateStatus('Sunucuya bağlanıyor...');

            await this.connection.start({
                transport: ['webSockets', 'serverSentEvents', 'longPolling'],
                waitForPageLoad: true
            });
            
            this.updateStatus('Sunucuya bağlandı');
            await this.logEvent('signalr_connected', 'SignalR connection established');

            // Join room
            await this.hub.invoke('joinRoom', this.config.roomId);
            this.updateStatus('Odaya katıldı - Karşı taraf bekleniyor...');
            
        } catch (error) {
            console.error('SignalR connection error:', error);
            await this.logEvent('signalr_error', `SignalR connection failed: ${error.message}`);
            throw new Error('Sunucu bağlantısı kurulamadı');
        }
    }

    // Handle signaling messages
    handleSignalingMessage(message) {
        try {
            const data = JSON.parse(message);
            console.log('Signaling message received:', data.type);

            switch (data.type) {
                case 'ready':
                    if (!this.isInitiator && !this.isStarted) {
                        this.maybeStart();
                    }
                    break;
                case 'offer':
                    this.handleOffer(data.offer);
                    break;
                case 'answer':
                    this.handleAnswer(data.answer);
                    break;
                case 'ice-candidate':
                    this.handleIceCandidate(data.candidate);
                    break;
                case 'bye':
                    this.handleRemoteHangup();
                    break;
            }
        } catch (error) {
            console.error('Signaling message error:', error);
        }
    }

    // Handle user joined
    handleUserJoined(adSoyad, konu) {
        console.log('User joined:', adSoyad, konu);
        
        this.participantCount = 2;
        this.updateParticipantCount();
        this.isChannelReady = true;

        if (!this.isInitiator && !this.isStarted) {
            this.isInitiator = true;
            this.updateStatus('Bağlantı kuruluyor...');
            
            setTimeout(() => {
                this.sendSignalingMessage({ type: 'ready' });
                this.maybeStart();
            }, 1000);
        } else if (!this.isStarted) {
            this.sendSignalingMessage({ type: 'ready' });
            this.maybeStart();
        }

        this.updateStatus(`${adSoyad} katıldı - ${konu}`);
        this.logEvent('user_joined', `${adSoyad} joined the call`);
    }

    // Handle user left
    handleUserLeft() {
        this.participantCount = 1;
        this.updateParticipantCount();
        this.handleRemoteHangup();
        this.logEvent('user_left', 'User left the call');
    }

    // Maybe start peer connection
    maybeStart() {
        console.log('maybeStart called, checking conditions:', {
            isStarted: this.isStarted,
            hasLocalStream: !!this.localStream,
            isChannelReady: this.isChannelReady
        });

        if (!this.isStarted && this.localStream && this.isChannelReady) {
            console.log('Starting peer connection...');
            this.createPeerConnection();
            this.isStarted = true;

            if (this.isInitiator) {
                console.log('Initiating call...');
                setTimeout(() => this.doCall(), 500);
            }
        }
    }

    // Create peer connection
    createPeerConnection() {
        try {
            this.peerConnection = new RTCPeerConnection(this.pcConfig);

            // Add local stream
            this.localStream.getTracks().forEach(track => {
                this.peerConnection.addTrack(track, this.localStream);
            });

            // Handle remote stream
            this.peerConnection.ontrack = (event) => {
                console.log('Remote track received');
                if (event.streams && event.streams[0]) {
                    this.remoteStream = event.streams[0];
                    this.remoteVideo.srcObject = this.remoteStream;
                    
                    this.remoteVideo.onloadedmetadata = () => {
                        this.remotePlaceholder.style.display = 'none';
                        this.updateStatus('Video bağlantısı kuruldu');
                        this.updateConnectionQuality('good');
                        this.logEvent('video_connected', 'Remote video stream connected');
                    };
                }
            };

            // Handle ICE candidates
            this.peerConnection.onicecandidate = (event) => {
                if (event.candidate) {
                    this.sendSignalingMessage({
                        type: 'ice-candidate',
