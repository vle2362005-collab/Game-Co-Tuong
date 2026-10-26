-- ============================================================================
-- DỰ ÁN GAME CỜ TƯỚNG (GCT)
-- HỆ CƠ SỞ DỮ LIỆU MICROSOFT SQL SERVER
-- Tương thích: SQL Server 2019 / 2022 / 2025 / Azure SQL
-- Chuẩn thiết kế: GCT-20 (Contracts & Architecture) & GCT-25 (Database ERD)
-- Lưu ý: Script này KHÔNG chứa DROP TABLE để tránh mất dữ liệu khi chạy lại.
--        Nếu cần reset sạch database từ đầu, vui lòng chạy: docs/database/reset.sql
-- ============================================================================

-- 1. Tạo Database (nếu chưa có)
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'GameCoTuong')
BEGIN
    CREATE DATABASE [GameCoTuong]
    COLLATE Vietnamese_100_CI_AS_SC_UTF8;
END
GO

USE [GameCoTuong];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

-- ============================================================================
-- 2. BẢNG Users (Người dùng & Tài khoản)
-- GCT-19 / FR-01: Username & Email unique sau khi chuẩn hóa (lowercase/trim)
-- Không lưu email trong hồ sơ công khai (UserPublic)
-- ============================================================================
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Users_Id DEFAULT NEWSEQUENTIALID(),
        Username NVARCHAR(50) NOT NULL,
        NormalizedUsername NVARCHAR(50) NOT NULL,
        Email NVARCHAR(256) NOT NULL,
        NormalizedEmail NVARCHAR(256) NOT NULL,
        PasswordHash NVARCHAR(500) NOT NULL,
        DisplayName NVARCHAR(100) NOT NULL,
        AvatarUrl NVARCHAR(500) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
        UpdatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Users_UpdatedAt DEFAULT SYSDATETIMEOFFSET(),
        IsActive BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT 1,

        CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Users_NormalizedUsername UNIQUE NONCLUSTERED (NormalizedUsername),
        CONSTRAINT UQ_Users_NormalizedEmail UNIQUE NONCLUSTERED (NormalizedEmail)
    );
END
GO

-- ============================================================================
-- 3. BẢNG Rooms (Phòng chơi)
-- BR-01 / BR-02: Mỗi phòng có 1 trận. Status: waiting, playing, finished, closed
-- Mã phòng (Code): 6 ký tự A-Z0-9, unique đối với phòng chưa đóng (status != 'closed')
-- ============================================================================
IF OBJECT_ID(N'dbo.Rooms', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Rooms (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Rooms_Id DEFAULT NEWSEQUENTIALID(),
        Code NVARCHAR(6) NOT NULL,
        Name NVARCHAR(80) NOT NULL,
        HostUserId UNIQUEIDENTIFIER NOT NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Rooms_Status DEFAULT N'waiting',
        Version BIGINT NOT NULL CONSTRAINT DF_Rooms_Version DEFAULT 1,
        
        -- Cấu hình thời gian (TimeControl)
        InitialMinutes INT NOT NULL CONSTRAINT DF_Rooms_InitialMinutes DEFAULT 15,
        IncrementSeconds INT NOT NULL CONSTRAINT DF_Rooms_IncrementSeconds DEFAULT 10,
        
        CurrentMatchId UNIQUEIDENTIFIER NULL, -- Sẽ tạo FK sau khi bảng Matches được tạo
        LastActivityAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Rooms_LastActivityAt DEFAULT SYSDATETIMEOFFSET(),
        CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Rooms_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
        ClosedAt DATETIMEOFFSET NULL,

        CONSTRAINT PK_Rooms PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Rooms_Users_HostUserId FOREIGN KEY (HostUserId) REFERENCES dbo.Users(Id),
        CONSTRAINT CK_Rooms_Status CHECK (Status IN (N'waiting', N'playing', N'finished', N'closed')),
        CONSTRAINT CK_Rooms_Code CHECK (LEN(Code) = 6 AND Code NOT LIKE N'%[^A-Z0-9]%')
    );
END
GO

-- Unique Index cho Code chỉ với các phòng đang hoạt động (chưa closed)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_Rooms_ActiveCode' AND object_id = OBJECT_ID(N'dbo.Rooms'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_Rooms_ActiveCode
    ON dbo.Rooms(Code)
    WHERE Status <> N'closed';
END
GO

-- ============================================================================
-- 4. BẢNG RoomMemberships (Thành viên / Ghế trong phòng)
-- Tối đa 2 người: Host là Đỏ (red), Guest là Đen (black)
-- BR-07: Một người chơi chỉ được thuộc 1 phòng chưa đóng
-- ============================================================================
IF OBJECT_ID(N'dbo.RoomMemberships', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoomMemberships (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_RoomMemberships_Id DEFAULT NEWSEQUENTIALID(),
        RoomId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        Side NVARCHAR(10) NOT NULL, -- 'red' hoặc 'black'
        IsReady BIT NOT NULL CONSTRAINT DF_RoomMemberships_IsReady DEFAULT 0,
        IsConnected BIT NOT NULL CONSTRAINT DF_RoomMemberships_IsConnected DEFAULT 1,
        ReconnectDeadlineAt DATETIMEOFFSET NULL,
        JoinedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_RoomMemberships_JoinedAt DEFAULT SYSDATETIMEOFFSET(),
        LeftAt DATETIMEOFFSET NULL,

        CONSTRAINT PK_RoomMemberships PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_RoomMemberships_Rooms_RoomId FOREIGN KEY (RoomId) REFERENCES dbo.Rooms(Id),
        CONSTRAINT FK_RoomMemberships_Users_UserId FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
        CONSTRAINT CK_RoomMemberships_Side CHECK (Side IN (N'red', N'black'))
    );
END
GO

-- Mỗi phòng tại một thời điểm chỉ có tối đa 1 người ở mỗi Side (nếu chưa rời)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_RoomMemberships_Room_Side_Active' AND object_id = OBJECT_ID(N'dbo.RoomMemberships'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_RoomMemberships_Room_Side_Active
    ON dbo.RoomMemberships(RoomId, Side)
    WHERE LeftAt IS NULL;
END
GO

-- Mỗi user tại một thời điểm chỉ ở 1 ghế phòng (chưa rời)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_RoomMemberships_User_Active' AND object_id = OBJECT_ID(N'dbo.RoomMemberships'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_RoomMemberships_User_Active
    ON dbo.RoomMemberships(UserId)
    WHERE LeftAt IS NULL;
END
GO

-- ============================================================================
-- 5. BẢNG Matches (Trận đấu)
-- BR-01 / BR-02: Mỗi phòng có 1 trận. Status: playing, paused, finished, cancelled
-- Lưu Snapshot bàn cờ, đồng hồ, ngân sách phục hồi 60s, đề nghị hòa
-- ============================================================================
IF OBJECT_ID(N'dbo.Matches', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Matches (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Matches_Id DEFAULT NEWSEQUENTIALID(),
        RoomId UNIQUEIDENTIFIER NOT NULL,
        Mode NVARCHAR(20) NOT NULL CONSTRAINT DF_Matches_Mode DEFAULT N'pvp', -- 'pvp' (v1), 'pve' (v2)
        RuleSetVersion NVARCHAR(50) NOT NULL CONSTRAINT DF_Matches_RuleSetVersion DEFAULT N'gct-core-1',
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_Matches_Status DEFAULT N'playing',
        Version BIGINT NOT NULL CONSTRAINT DF_Matches_Version DEFAULT 1,
        MoveNumber INT NOT NULL CONSTRAINT DF_Matches_MoveNumber DEFAULT 0, -- Số ply đã đi
        SideToMove NVARCHAR(10) NOT NULL CONSTRAINT DF_Matches_SideToMove DEFAULT N'red',

        -- Trạng thái bàn cờ (JSON snapshot các quân cờ đang còn trên bàn)
        InitialBoardJson NVARCHAR(MAX) NOT NULL, -- 32 quân ban đầu
        CurrentBoardJson NVARCHAR(MAX) NOT NULL, -- snapshot hiện tại

        -- Đồng hồ (Clock)
        RedRemainingMs INT NOT NULL,
        BlackRemainingMs INT NOT NULL,
        TurnStartedAt DATETIMEOFFSET NULL, -- NULL khi paused hoặc terminal

        -- Ngân sách phục hồi kết nối (Reconnect Recovery) - BR-04, BR-05, BR-08
        RecoveryReason NVARCHAR(20) NULL, -- 'disconnect', 'serverRestart', hoặc NULL
        RedRecoveryRemainingMs INT NOT NULL CONSTRAINT DF_Matches_RedRecoveryRemainingMs DEFAULT 60000,
        RedDisconnectedAt DATETIMEOFFSET NULL,
        RedDeadlineAt DATETIMEOFFSET NULL,
        BlackRecoveryRemainingMs INT NOT NULL CONSTRAINT DF_Matches_BlackRecoveryRemainingMs DEFAULT 60000,
        BlackDisconnectedAt DATETIMEOFFSET NULL,
        BlackDeadlineAt DATETIMEOFFSET NULL,

        -- Đề nghị hòa tạm thời (Pending Draw Offer) - BR-10
        DrawOfferId UNIQUEIDENTIFIER NULL,
        DrawOfferedBy NVARCHAR(10) NULL, -- 'red' hoặc 'black'
        DrawCreatedAt DATETIMEOFFSET NULL,
        DrawExpiresAt DATETIMEOFFSET NULL,

        -- Kết quả trận đấu (Result)
        Winner NVARCHAR(10) NULL, -- 'red', 'black', hoặc NULL (hòa / hủy)
        ResultReason NVARCHAR(30) NULL, -- 'checkmate', 'stalemate', 'resignation', 'timeout', 'disconnect', 'agreement', 'bothDisconnected', 'serverRestart'

        StartedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Matches_StartedAt DEFAULT SYSDATETIMEOFFSET(),
        EndedAt DATETIMEOFFSET NULL,

        CONSTRAINT PK_Matches PRIMARY KEY CLUSTERED (Id),
        -- FK tới Rooms: KHÔNG CASCADE DELETE để bảo toàn dữ liệu lịch sử khi đóng phòng
        CONSTRAINT FK_Matches_Rooms_RoomId FOREIGN KEY (RoomId) REFERENCES dbo.Rooms(Id) ON DELETE NO ACTION,
        CONSTRAINT CK_Matches_Status CHECK (Status IN (N'playing', N'paused', N'finished', N'cancelled')),
        CONSTRAINT CK_Matches_SideToMove CHECK (SideToMove IN (N'red', N'black')),
        CONSTRAINT CK_Matches_RecoveryReason CHECK (RecoveryReason IS NULL OR RecoveryReason IN (N'disconnect', N'serverRestart')),
        CONSTRAINT CK_Matches_Winner CHECK (Winner IS NULL OR Winner IN (N'red', N'black')),
        CONSTRAINT CK_Matches_ResultReason CHECK (ResultReason IS NULL OR ResultReason IN (
            N'checkmate', N'stalemate', N'resignation', N'timeout', N'disconnect',
            N'agreement', N'bothDisconnected', N'serverRestart'
        ))
    );
END
GO

-- Liên kết FK từ Rooms.CurrentMatchId tới Matches.Id (nếu chưa có)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Rooms_Matches_CurrentMatchId' AND parent_object_id = OBJECT_ID(N'dbo.Rooms'))
BEGIN
    ALTER TABLE dbo.Rooms
    ADD CONSTRAINT FK_Rooms_Matches_CurrentMatchId
    FOREIGN KEY (CurrentMatchId) REFERENCES dbo.Matches(Id) ON DELETE NO ACTION;
END
GO

-- ============================================================================
-- 6. BẢNG MatchPlayers (Người chơi tham gia trận đấu)
-- Tách biệt người chơi lịch sử của trận đấu với ghế hiện tại của phòng (BR-01)
-- ============================================================================
IF OBJECT_ID(N'dbo.MatchPlayers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MatchPlayers (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_MatchPlayers_Id DEFAULT NEWSEQUENTIALID(),
        MatchId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        Side NVARCHAR(10) NOT NULL, -- 'red' hoặc 'black'

        CONSTRAINT PK_MatchPlayers PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_MatchPlayers_Matches_MatchId FOREIGN KEY (MatchId) REFERENCES dbo.Matches(Id) ON DELETE NO ACTION,
        CONSTRAINT FK_MatchPlayers_Users_UserId FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
        CONSTRAINT CK_MatchPlayers_Side CHECK (Side IN (N'red', N'black')),
        CONSTRAINT UQ_MatchPlayers_Match_Side UNIQUE NONCLUSTERED (MatchId, Side),
        CONSTRAINT UQ_MatchPlayers_Match_User UNIQUE NONCLUSTERED (MatchId, UserId)
    );
END
GO

-- ============================================================================
-- 7. BẢNG Moves (Nước đi)
-- Lưu trữ đầy đủ để phát lại (Replay) từ bàn cờ ban đầu
-- Chống trùng lặp tuyệt đối qua:
--   1. UQ (MatchId, Ply) -> Mỗi ply chỉ có 1 nước đi duy nhất
--   2. UQ (MatchId, RequestId) -> Cùng requestId gửi lại không ghi nước mới
-- ============================================================================
IF OBJECT_ID(N'dbo.Moves', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Moves (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Moves_Id DEFAULT NEWSEQUENTIALID(),
        MatchId UNIQUEIDENTIFIER NOT NULL,
        Ply INT NOT NULL, -- Nước thứ mấy trong trận (1, 2, 3...)
        RequestId UNIQUEIDENTIFIER NOT NULL,
        MatchVersion BIGINT NOT NULL,
        Side NVARCHAR(10) NOT NULL, -- 'red' hoặc 'black'
        PieceId NVARCHAR(32) NOT NULL, -- ví dụ 'red-soldier-1', 'black-cannon-2'
        FromCol INT NOT NULL,
        FromRow INT NOT NULL,
        ToCol INT NOT NULL,
        ToRow INT NOT NULL,
        CapturedPieceId NVARCHAR(32) NULL, -- Quân bị ăn (nếu có)
        BoardSnapshotJson NVARCHAR(MAX) NULL, -- Snapshot bàn cờ ngay sau nước đi (tùy chọn để tối ưu sync/replay)
        CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Moves_CreatedAt DEFAULT SYSDATETIMEOFFSET(),

        CONSTRAINT PK_Moves PRIMARY KEY CLUSTERED (Id),
        -- FK tới Matches: KHÔNG CASCADE DELETE
        CONSTRAINT FK_Moves_Matches_MatchId FOREIGN KEY (MatchId) REFERENCES dbo.Matches(Id) ON DELETE NO ACTION,
        CONSTRAINT CK_Moves_Side CHECK (Side IN (N'red', N'black')),
        CONSTRAINT CK_Moves_FromCol CHECK (FromCol BETWEEN 0 AND 8),
        CONSTRAINT CK_Moves_FromRow CHECK (FromRow BETWEEN 0 AND 9),
        CONSTRAINT CK_Moves_ToCol CHECK (ToCol BETWEEN 0 AND 8),
        CONSTRAINT CK_Moves_ToRow CHECK (ToRow BETWEEN 0 AND 9),
        CONSTRAINT UQ_Moves_Match_Ply UNIQUE NONCLUSTERED (MatchId, Ply),
        CONSTRAINT UQ_Moves_Match_RequestId UNIQUE NONCLUSTERED (MatchId, RequestId)
    );
END
GO

-- ============================================================================
-- 8. BẢNG CommandReceipts (Biên nhận lệnh - Idempotency)
-- GCT-20 / Mục 5: Ghi trong CÙNG TRANSACTION với tác dụng của lệnh
-- Giúp xử lý retry an toàn khi client timeout hoặc mất mạng
-- ============================================================================
IF OBJECT_ID(N'dbo.CommandReceipts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CommandReceipts (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_CommandReceipts_Id DEFAULT NEWSEQUENTIALID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        RequestId UNIQUEIDENTIFIER NOT NULL,
        Operation NVARCHAR(50) NOT NULL, -- ví dụ: 'SubmitMove', 'StartMatch', 'Resign', 'OfferDraw'
        TargetId UNIQUEIDENTIFIER NULL, -- RoomId hoặc MatchId
        Fingerprint NVARCHAR(64) NOT NULL, -- Hash payload để phát hiện request giả mạo/đổi ruột
        StatusCode INT NOT NULL, -- HTTP status code thành công ban đầu (200, 201)
        ResponseBody NVARCHAR(MAX) NOT NULL, -- JSON response gốc đã trả về
        CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_CommandReceipts_CreatedAt DEFAULT SYSDATETIMEOFFSET(),

        CONSTRAINT PK_CommandReceipts PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_CommandReceipts_Users_UserId FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
        CONSTRAINT UQ_CommandReceipts_User_RequestId UNIQUE NONCLUSTERED (UserId, RequestId)
    );
END
GO

-- ============================================================================
-- 9. BẢNG ChatMessages (Tin nhắn trò chuyện trong phòng)
-- GCT-19 / FR-08: 1-500 ký tự, sequence tăng dần theo phòng để client tải bù
-- ============================================================================
IF OBJECT_ID(N'dbo.ChatMessages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChatMessages (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ChatMessages_Id DEFAULT NEWSEQUENTIALID(),
        RoomId UNIQUEIDENTIFIER NOT NULL,
        Sequence BIGINT NOT NULL, -- Số thứ tự tin nhắn tăng dần trong phòng (1, 2, 3...)
        SenderUserId UNIQUEIDENTIFIER NOT NULL,
        Text NVARCHAR(500) NOT NULL,
        RequestId UNIQUEIDENTIFIER NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_ChatMessages_CreatedAt DEFAULT SYSDATETIMEOFFSET(),

        CONSTRAINT PK_ChatMessages PRIMARY KEY CLUSTERED (Id),
        -- FK tới Rooms: KHÔNG CASCADE DELETE
        CONSTRAINT FK_ChatMessages_Rooms_RoomId FOREIGN KEY (RoomId) REFERENCES dbo.Rooms(Id) ON DELETE NO ACTION,
        CONSTRAINT FK_ChatMessages_Users_SenderUserId FOREIGN KEY (SenderUserId) REFERENCES dbo.Users(Id),
        CONSTRAINT UQ_ChatMessages_Room_Sequence UNIQUE NONCLUSTERED (RoomId, Sequence),
        CONSTRAINT UQ_ChatMessages_Room_RequestId UNIQUE NONCLUSTERED (RoomId, RequestId)
    );
END
GO

-- ============================================================================
-- 10. BẢNG OutboxMessages (Mẫu Outbox - Đảm bảo tính nhất quán sự kiện)
-- Lưu sự kiện trong transaction cùng nghiệp vụ, Worker đọc để phát qua SignalR
-- ============================================================================
IF OBJECT_ID(N'dbo.OutboxMessages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OutboxMessages (
        EventId UNIQUEIDENTIFIER NOT NULL,
        AggregateType NVARCHAR(30) NOT NULL, -- 'room', 'match', 'chat'
        AggregateId UNIQUEIDENTIFIER NOT NULL,
        Version BIGINT NOT NULL,
        Type NVARCHAR(50) NOT NULL, -- 'RoomUpdated', 'MatchUpdated', 'ChatMessageCreated'
        Payload NVARCHAR(MAX) NOT NULL, -- JSON schema ServerEvent
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_OutboxMessages_Status DEFAULT N'pending', -- 'pending', 'published', 'failed'
        RetryCount INT NOT NULL CONSTRAINT DF_OutboxMessages_RetryCount DEFAULT 0,
        LastError NVARCHAR(MAX) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_OutboxMessages_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
        ProcessedAt DATETIMEOFFSET NULL,

        CONSTRAINT PK_OutboxMessages PRIMARY KEY CLUSTERED (EventId),
        CONSTRAINT CK_OutboxMessages_Status CHECK (Status IN (N'pending', N'published', N'failed'))
    );
END
GO

-- Index phục vụ Background Worker quét các event chưa phát
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OutboxMessages_Pending' AND object_id = OBJECT_ID(N'dbo.OutboxMessages'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_OutboxMessages_Pending
    ON dbo.OutboxMessages(Status, CreatedAt)
    INCLUDE (AggregateType, AggregateId, Version)
    WHERE Status = N'pending';
END
GO

-- ============================================================================
-- 11. BẢNG RevokedTokens (Thu hồi JWT Token khi Logout)
-- Lưu JTI đến thời điểm token hết hạn tự nhiên để chặn dùng lại
-- ============================================================================
IF OBJECT_ID(N'dbo.RevokedTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RevokedTokens (
        Jti NVARCHAR(64) NOT NULL, -- JWT ID
        UserId UNIQUEIDENTIFIER NOT NULL,
        ExpiresAt DATETIMEOFFSET NOT NULL,
        RevokedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_RevokedTokens_RevokedAt DEFAULT SYSDATETIMEOFFSET(),

        CONSTRAINT PK_RevokedTokens PRIMARY KEY CLUSTERED (Jti),
        CONSTRAINT FK_RevokedTokens_Users_UserId FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
    );
END
GO

-- Index dọn dẹp các token đã quá hạn
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RevokedTokens_ExpiresAt' AND object_id = OBJECT_ID(N'dbo.RevokedTokens'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_RevokedTokens_ExpiresAt
    ON dbo.RevokedTokens(ExpiresAt);
END
GO

-- ============================================================================
-- 12. DỮ LIỆU SEED MẪU (Demo Seed Data - Dùng cho Development / Testing)
-- Mật khẩu mã hóa giả lập (SHA256 demo placeholder cho test môi trường)
-- ============================================================================
BEGIN TRANSACTION;

-- Seed Users
DECLARE @UserRedId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @UserBlackId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222222';

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Id = @UserRedId)
BEGIN
    INSERT INTO dbo.Users (Id, Username, NormalizedUsername, Email, NormalizedEmail, PasswordHash, DisplayName)
    VALUES 
    (@UserRedId, N'kythu_do', N'KYTHU_DO', N'red@cotuong.local', N'RED@COTUONG.LOCAL', N'AQAAAAEAACcQAAAAEPlaceholderPasswordHashRedForLocalTesting123456789==', N'Kỳ Thủ Đỏ'),
    (@UserBlackId, N'kythu_den', N'KYTHU_DEN', N'black@cotuong.local', N'BLACK@COTUONG.LOCAL', N'AQAAAAEAACcQAAAAEPlaceholderPasswordHashBlackForLocalTesting123456789==', N'Kỳ Thủ Đen');
END

COMMIT TRANSACTION;
GO

PRINT N'Cơ sở dữ liệu GameCoTuong đã được thiết lập thành công trên Microsoft SQL Server!';
GO
