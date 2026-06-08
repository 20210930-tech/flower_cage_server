namespace FlowerCageServer.Common.Enums;

public enum CommandType
{
    Water = 0,
    Led = 1,
    AutoMode = 2,
    Alert = 3,
    ResetConfig = 4 // 기기 설정 초기화 (EEPROM 삭제 후 재부팅 → BLE 설정 모드)
}
