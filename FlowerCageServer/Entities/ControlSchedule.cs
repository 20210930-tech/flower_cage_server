using FlowerCageServer.Common.Enums;

namespace FlowerCageServer.Entities;

// 시간 기반 예약. 서버 ScheduleBackgroundService가 주기적으로 확인해
// 발화 시각에 ControlCommand를 생성한다. (수동 모드에서만 발화)
public class ControlSchedule : BaseEntity
{
    public Guid CageDeviceId { get; set; }
    public Guid? PlantProfileId { get; set; }

    public ScheduleType Type { get; set; }
    public ScheduleRepeat Repeat { get; set; }
    public bool Enabled { get; set; } = true;
    public string? Label { get; set; }

    // 발화 시각 (KST 기준 벽시계)
    public int Hour { get; set; }
    public int Minute { get; set; }

    // 반복별 부가 정보
    public int? DayOfWeek { get; set; }  // Weekly: 0=일 ~ 6=토
    public int? DayOfMonth { get; set; } // Monthly/Yearly: 1~31
    public int? Month { get; set; }      // Yearly: 1~12
    public DateTime? Date { get; set; }  // Once: 실행 날짜

    // 액션 값
    public int? WaterDurationSec { get; set; } // Water
    public int? LedR { get; set; }             // Led
    public int? LedG { get; set; }
    public int? LedB { get; set; }

    // 중복 발화 방지 (마지막 발화 시각, UTC)
    public DateTime? LastFiredAt { get; set; }
}
