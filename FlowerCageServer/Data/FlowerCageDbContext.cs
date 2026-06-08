using Microsoft.EntityFrameworkCore;
using FlowerCageServer.Entities;

namespace FlowerCageServer.Data;

public class FlowerCageDbContext : DbContext
{
    public FlowerCageDbContext(DbContextOptions<FlowerCageDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<CageDevice> CageDevices => Set<CageDevice>();
    public DbSet<PlantProfile> PlantProfiles => Set<PlantProfile>();
    public DbSet<PlantEnvironmentSetting> PlantEnvironmentSettings => Set<PlantEnvironmentSetting>();
    public DbSet<SensorReading> SensorReadings => Set<SensorReading>();
    public DbSet<ControlCommand> ControlCommands => Set<ControlCommand>();
    public DbSet<AlertEvent> AlertEvents => Set<AlertEvent>();
    public DbSet<GrowthLog> GrowthLogs => Set<GrowthLog>();
    public DbSet<GptAnalysisLog> GptAnalysisLogs => Set<GptAnalysisLog>();
    public DbSet<SystemEventLog> SystemEventLogs => Set<SystemEventLog>();
    public DbSet<ControlSchedule> ControlSchedules => Set<ControlSchedule>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var trackedEntries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in trackedEntries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<CageDevice>(entity =>
        {
            entity.Property(x => x.DeviceName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.DeviceSerial).HasMaxLength(100).IsRequired();
            entity.Property(x => x.WifiStatus).HasMaxLength(50);
            entity.HasIndex(x => x.DeviceSerial).IsUnique();
        });

        modelBuilder.Entity<PlantProfile>(entity =>
        {
            entity.Property(x => x.PlantName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Species).HasMaxLength(100);
            entity.Property(x => x.Memo).HasMaxLength(1000);

            // 식물 삭제 시, 이를 참조하는 (nullable) 센서·GPT 로그는 막지 말고 FK를 null 처리
            entity.HasMany(x => x.SensorReadings)
                .WithOne(s => s.PlantProfile)
                .HasForeignKey(s => s.PlantProfileId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(x => x.GptAnalysisLogs)
                .WithOne(g => g.PlantProfile)
                .HasForeignKey(g => g.PlantProfileId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PlantEnvironmentSetting>(entity =>
        {
            entity.HasIndex(x => x.PlantProfileId).IsUnique();
        });

        // 개별 센서 원시값 배열 → PostgreSQL numeric[]
        modelBuilder.Entity<SensorReading>(entity =>
        {
            entity.Property(x => x.SoilMoistures).HasColumnType("numeric[]");
            entity.Property(x => x.Temperatures).HasColumnType("numeric[]");
            entity.Property(x => x.Humidities).HasColumnType("numeric[]");
        });

        modelBuilder.Entity<ControlCommand>(entity =>
        {
            entity.Property(x => x.CommandType).HasConversion<string>();
            entity.Property(x => x.SourceType).HasConversion<string>();
            entity.Property(x => x.Status).HasConversion<string>();
            entity.Property(x => x.Reason).HasMaxLength(500);
        });

        modelBuilder.Entity<AlertEvent>(entity =>
        {
            entity.Property(x => x.Level).HasConversion<string>();
            entity.Property(x => x.Title).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Source).HasMaxLength(100);

            // 기기·식물 삭제 시 알림은 막지 말고 FK를 null 처리
            entity.HasOne(x => x.PlantProfile)
                .WithMany()
                .HasForeignKey(x => x.PlantProfileId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.CageDevice)
                .WithMany()
                .HasForeignKey(x => x.CageDeviceId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<GrowthLog>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Content).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.CameraImagePlaceholderPath).HasMaxLength(500);
            entity.Property(x => x.CameraImagePublicUrl).HasMaxLength(500);
        });

        modelBuilder.Entity<GptAnalysisLog>(entity =>
        {
            entity.Property(x => x.Status).HasConversion<string>();
            entity.Property(x => x.AnalysisType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PromptSummary).HasMaxLength(1000);
            entity.Property(x => x.ResponseContent).HasMaxLength(4000);
            entity.Property(x => x.Recommendation).HasMaxLength(2000);

            // 센서 기록 삭제 시 GPT 로그는 막지 말고 FK를 null 처리
            entity.HasOne(x => x.SensorReading)
                .WithMany()
                .HasForeignKey(x => x.SensorReadingId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SystemEventLog>(entity =>
        {
            entity.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EventSource).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.PayloadJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<ControlSchedule>(entity =>
        {
            entity.Property(x => x.Type).HasConversion<string>();
            entity.Property(x => x.Repeat).HasConversion<string>();
            entity.Property(x => x.Label).HasMaxLength(100);
            entity.HasIndex(x => x.CageDeviceId);
        });
    }
}
