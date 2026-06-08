using FlowerCageServer.Common.Enums;

namespace FlowerCageServer.Entities;

public class ControlCommand : BaseEntity
{
    public Guid CageDeviceId { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public CommandType CommandType { get; set; }
    public bool? WaterPumpOn { get; set; }
    public decimal? WaterAmountMl { get; set; } // (레거시) ml 기준 — 호환용 보관
    public int? WaterDurationSec { get; set; }  // 급수 시간(초) — 펌프는 초 단위로 동작
    public bool? LedOn { get; set; }
    public int? LedBrightness { get; set; }      // (레거시) 0~100% — 호환용 보관
    public int? LedR { get; set; }               // LED RGB (0~255)
    public int? LedG { get; set; }
    public int? LedB { get; set; }
    public bool? AutoModeOn { get; set; }
    public bool? AlertEnabled { get; set; }
    public string? Reason { get; set; }
    public SourceType SourceType { get; set; }
    public CommandStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? AppliedAt { get; set; }

    public CageDevice? CageDevice { get; set; }
    public User? RequestedByUser { get; set; }
}
