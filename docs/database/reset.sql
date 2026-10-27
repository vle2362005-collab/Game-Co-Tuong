-- ============================================================================
-- DỰ ÁN GAME CỜ TƯỚNG (GCT)
-- SCRIPT RESET TOÀN BỘ CƠ SỞ DỮ LIỆU
-- Tương thích: Microsoft SQL Server 2019 / 2022 / 2025 / Azure SQL
-- Chức năng: Xóa sạch toàn bộ bảng và dữ liệu theo đúng thứ tự khóa ngoại
-- CẢNH BÁO: CHỈ DÙNG TRONG MÔI TRƯỜNG TEST / DEV HOẶC KHI CẦN KHỞI TẠO LẠI TỪ ĐẦU!
--           KHÔNG CHẠY TRÊN MÔI TRƯỜNG PRODUCTION!
-- ============================================================================

USE [GameCoTuong];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT N'----------------------------------------------------------------------------';
PRINT N'Bắt đầu dọn dẹp các bảng trong cơ sở dữ liệu GameCoTuong...';
PRINT N'----------------------------------------------------------------------------';

-- 1. Xóa các bảng độc lập / lá (Leaf tables) không bị bảng khác trỏ khóa ngoại
IF OBJECT_ID(N'dbo.OutboxMessages', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.OutboxMessages;
    PRINT N'-> Đã xóa bảng dbo.OutboxMessages';
END
GO

IF OBJECT_ID(N'dbo.RevokedTokens', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.RevokedTokens;
    PRINT N'-> Đã xóa bảng dbo.RevokedTokens';
END
GO

IF OBJECT_ID(N'dbo.CommandReceipts', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.CommandReceipts;
    PRINT N'-> Đã xóa bảng dbo.CommandReceipts';
END
GO

IF OBJECT_ID(N'dbo.ChatMessages', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.ChatMessages;
    PRINT N'-> Đã xóa bảng dbo.ChatMessages';
END
GO

IF OBJECT_ID(N'dbo.Moves', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.Moves;
    PRINT N'-> Đã xóa bảng dbo.Moves';
END
GO

IF OBJECT_ID(N'dbo.MatchPlayers', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.MatchPlayers;
    PRINT N'-> Đã xóa bảng dbo.MatchPlayers';
END
GO

-- 2. Gỡ bỏ khóa ngoại vòng giữa Rooms và Matches trước khi xóa Matches
IF OBJECT_ID(N'dbo.Rooms', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Matches', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Rooms_Matches_CurrentMatchId')
    BEGIN
        ALTER TABLE dbo.Rooms DROP CONSTRAINT FK_Rooms_Matches_CurrentMatchId;
        PRINT N'-> Đã gỡ bỏ ràng buộc khóa ngoại FK_Rooms_Matches_CurrentMatchId';
    END
END
GO

-- 3. Xóa bảng Matches
IF OBJECT_ID(N'dbo.Matches', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.Matches;
    PRINT N'-> Đã xóa bảng dbo.Matches';
END
GO

-- 4. Xóa bảng RoomMemberships
IF OBJECT_ID(N'dbo.RoomMemberships', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.RoomMemberships;
    PRINT N'-> Đã xóa bảng dbo.RoomMemberships';
END
GO

-- 5. Xóa bảng Rooms
IF OBJECT_ID(N'dbo.Rooms', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.Rooms;
    PRINT N'-> Đã xóa bảng dbo.Rooms';
END
GO

-- 6. Xóa bảng gốc Users
IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.Users;
    PRINT N'-> Đã xóa bảng dbo.Users';
END
GO

PRINT N'----------------------------------------------------------------------------';
PRINT N'Đã hoàn tất dọn sạch toàn bộ 10 bảng của cơ sở dữ liệu GameCoTuong.';
PRINT N'Để tạo lại schema, vui lòng chạy file: docs/database/schema.sql';
PRINT N'----------------------------------------------------------------------------';
GO
