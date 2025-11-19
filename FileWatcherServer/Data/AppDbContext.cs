using Microsoft.EntityFrameworkCore;
using FileWatcherServer.Models;
using System.Text.Json;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace FileWatcherServer.Data;

public class AppDbContext : DbContext
{
    public DbSet<EventBlock> EventBlocks => Set<EventBlock>();
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var genesisData = JsonSerializer.Serialize(new List<FileEvent>());
        var genesis = new EventBlock(new DateTime(2025, 1, 1), "0", genesisData);
        genesis.Hash = genesis.CalculateHash();

        // Gán Id = 1 cho genesis để bạn có thể lọc b.Id > 1 cho block thường
        modelBuilder.Entity<EventBlock>().HasData(new EventBlock
        {
            Id = 1,
            Timestamp = genesis.Timestamp,
            PreviousHash = genesis.PreviousHash,
            Data = genesis.Data,
            Hash = genesis.Hash
        });
    }
}
