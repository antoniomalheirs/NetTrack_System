using Dapper;
using NetTrack.Application.Interfaces;
using NetTrack.Domain.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NetTrack.Infrastructure.Services
{
    public class StorageService : IStorageService
    {
        private const string DbFile = "NetTrack.db";
        private string ConnectionString => $"Data Source={DbFile};Version=3;";
        private readonly ILogger<StorageService> _logger;

        public StorageService(ILogger<StorageService> logger)
        {
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            if (!File.Exists(DbFile))
            {
                SQLiteConnection.CreateFile(DbFile);
            }

            using var conn = new SQLiteConnection(ConnectionString);
            await conn.OpenAsync();

            var sql = @"
                CREATE TABLE IF NOT EXISTS Sessions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    StartTime TEXT NOT NULL,
                    EndTime TEXT,
                    InterfaceName TEXT NOT NULL,
                    PacketCount INTEGER NOT NULL DEFAULT 0,
                    Status TEXT NOT NULL DEFAULT 'Active'
                );

                CREATE TABLE IF NOT EXISTS Packets (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SessionId INTEGER NOT NULL,
                    Timestamp TEXT NOT NULL,
                    SourceIP TEXT,
                    DestinationIP TEXT,
                    SourcePort INTEGER,
                    DestinationPort INTEGER,
                    Protocol TEXT,
                    Length INTEGER,
                    Info TEXT,
                    OriginalData BLOB,
                    FOREIGN KEY(SessionId) REFERENCES Sessions(Id)
                );";

            await conn.ExecuteAsync(sql);
        }

        public async Task<int> SaveSessionAsync(SessionModel session)
        {
            using var conn = new SQLiteConnection(ConnectionString);
            var sql = @"
                INSERT INTO Sessions (StartTime, InterfaceName, PacketCount, Status)
                VALUES (@StartTime, @InterfaceName, 0, 'Active');
                SELECT last_insert_rowid();";
            
            _logger.LogInformation("New session created on interface: {Interface}", session.InterfaceName);
            return await conn.QuerySingleAsync<int>(sql, session);
        }

        public async Task SavePacketsAsync(int sessionId, IEnumerable<PacketModel> packets)
        {
            using var conn = new SQLiteConnection(ConnectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();

            var sql = @"
                INSERT INTO Packets (SessionId, Timestamp, SourceIP, DestinationIP, SourcePort, DestinationPort, Protocol, Length, Info, OriginalData)
                VALUES (@SessionId, @Timestamp, @SourceIP, @DestinationIP, @SourcePort, @DestinationPort, @Protocol, @Length, @Info, @OriginalData)";

            foreach (var p in packets)
            {
                await conn.ExecuteAsync(sql, new { 
                    SessionId = sessionId,
                    p.Timestamp,
                    p.SourceIP,
                    p.DestinationIP,
                    p.SourcePort,
                    p.DestinationPort,
                    p.Protocol,
                    p.Length,
                    p.Info,
                    p.OriginalData
                }, transaction);
            }

            transaction.Commit();
            _logger.LogDebug("Saved batch of {Count} packets to session {SessionId}", ((List<PacketModel>)packets).Count, sessionId);
        }

        public async Task UpdateSessionAsync(SessionModel session)
        {
            using var conn = new SQLiteConnection(ConnectionString);
            var sql = "UPDATE Sessions SET EndTime = @EndTime, PacketCount = @PacketCount, Status = @Status WHERE Id = @Id";
            await conn.ExecuteAsync(sql, session);
            _logger.LogInformation("Session {SessionId} updated with status: {Status}", session.Id, session.Status);
        }

        public async Task<IEnumerable<SessionModel>> GetSessionsAsync()
        {
            using var conn = new SQLiteConnection(ConnectionString);
            return await conn.QueryAsync<SessionModel>("SELECT * FROM Sessions ORDER BY StartTime DESC");
        }

        public async Task<IEnumerable<PacketModel>> GetPacketsAsync(int sessionId)
        {
            using var conn = new SQLiteConnection(ConnectionString);
            return await conn.QueryAsync<PacketModel>("SELECT * FROM Packets WHERE SessionId = @sessionId", new { sessionId });
        }
    }
}
