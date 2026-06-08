namespace FlowerCageServer.Common.Enums;

// 제어 모드 3종:
//  Manual : 자동화 없음 (수동 명령만)
//  Auto   : 서버 퍼지 로직 제어 (AutoControlService + FuzzyControlEngine)
//  Ai     : 주기적으로 AI가 센서/상태 기반 제어 (AiControlBackgroundService)
public enum ControlMode
{
    Manual = 0,
    Auto = 1,
    Ai = 2
}
