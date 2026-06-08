namespace FlowerCageServer.Common.Enums;

// 예약 반복 주기
public enum ScheduleRepeat
{
    Once = 0,    // 1회성 (Date 사용)
    Daily = 1,   // 매일
    Weekly = 2,  // 매주 (DayOfWeek 사용, 0=일~6=토)
    Monthly = 3, // 매달 (DayOfMonth 사용)
    Yearly = 4   // 매년 (Month + DayOfMonth 사용)
}
