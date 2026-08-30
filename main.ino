// 필요 라이브러리: DHT, WiFiNINA, ArduinoBLE, Adafruit_NeoPixel, Adafruit_AS7341, FlashStorage_SAMD
// 모듈: dht_sensor.ino / soil_sensor.ino / spectrum_sensor.ino / led.ino / pump.ino
// 네트워크: network.ino / BLE_Setup.ino  /  데이터: sensor.ino

#include <SPI.h>
#include <WiFiNINA.h>
#include <ArduinoBLE.h>

// ═══════════════════════════════════════════════════════════
//  센서 구성  ← 여기만 수정하세요
//  센서 개수를 바꾸면 자동으로 모든 값이 서버로 전송됩니다.
//  평균 계산 및 저장은 서버에서 처리합니다.
// ═══════════════════════════════════════════════════════════

// 토양 수분 센서
#define NUM_SOIL_SENSORS  3
int soilSensorPins[] = {A1, A2, A3};
// 예) 2개: #define NUM_SOIL_SENSORS 2
//          int soilSensorPins[] = {A1, A2};

// 온/습도 센서 (핀은 dht_sensor.ino 의 DHTPIN1=D2, DHTPIN2=D3 에서 수정)
#define NUM_DHT_SENSORS 2   // 1 또는 2

// LED 구역 수
#define NUM_LED_ZONES 13

// ═══════════════════════════════════════════════════════════

// 네트워크 설정
char ssid[32]         = "";
char pass[64]         = "";
// Cloudflare 터널 호스트명 (http=80). connect()와 Host 헤더 양쪽에 이 값이 쓰인다.
const char* server    = "nominated-cornell-playback-barcelona.trycloudflare.com";
int  port             = 80;
int  status           = WL_IDLE_STATUS;
WiFiClient client;
// 부팅 후 한 번이라도 WiFi에 연결된 적이 있는지. 잘못된 비밀번호(처음부터 실패)와
// 일시적 공유기 끊김(연결됐다가 끊김)을 구분해, 멀쩡한 자격증명을 지우지 않기 위함이다.
bool wifiEverConnected = false;

// 장치 식별자 (BLE 초기 설정 시 수신)
char cageDeviceId[37]  = "";
char plantProfileId[37] = "";

// 제어 명령 폴링 주기. 센서 전송 주기와 별도로 millis() 기반으로 확인한다.
#define COMMAND_POLL_INTERVAL_MS 2000UL
#define SENSOR_LOOP_DELAY_MS     5000UL

void setup() {
  Serial.begin(9600);
  unsigned long _t = millis();
  while (!Serial && millis() - _t < 3000) { ; }

  if (!loadWiFiFromEEPROM()) {
    Serial.println("저장된 WiFi 설정 없음 → BLE 초기 설정 모드");
    waitForWiFiInfoViaBLE();
  } else {
    Serial.println("EEPROM 로드 완료 / DeviceId: " + String(cageDeviceId));
  }

  // 식별자가 없으면 펌웨어가 직접 만들어 내부 저장소에 보관한다.
  // 한 번 만든 뒤에는 재부팅이나 전원 차단에도 같은 식별자를 계속 쓴다.
  ensureDeviceIds();

  connectToWiFi();
  setupDHT();
  setupSpectrum();
  setupLED();
  setupPump();
}

void loop() {
  // ── 와이파이 연결 점검 (끊겼으면 자동 재연결) ────────────
  ensureWiFiConnected();
  serviceControlCommands();

  // ── 센서 개별 읽기 (평균은 서버에서) ─────────────────────

// 토양 수분 센서
  int   soilValues[NUM_SOIL_SENSORS];

// 온/습도 센서 2개
  float tempValues[NUM_DHT_SENSORS];
  float humidValues[NUM_DHT_SENSORS];

// 토양 데이터 읽기
  readAllSoilSensors(soilValues);

  // 온/습도 데이터 읽기
  readAllTemperatures(tempValues);
  readAllHumidities(humidValues);

  // 시리얼 출력
  Serial.print("토양수분 [");
  for (int i = 0; i < NUM_SOIL_SENSORS; i++) {
    Serial.print(soilValues[i]);
    if (i < NUM_SOIL_SENSORS - 1) Serial.print(", ");
  }
  Serial.println("]");

  Serial.print("온도 [");
  for (int i = 0; i < NUM_DHT_SENSORS; i++) {
    Serial.print(tempValues[i], 1);
    if (i < NUM_DHT_SENSORS - 1) Serial.print(", ");
  }
  Serial.println("]C");

  Serial.print("습도 [");
  for (int i = 0; i < NUM_DHT_SENSORS; i++) {
    Serial.print(humidValues[i], 1);
    if (i < NUM_DHT_SENSORS - 1) Serial.print(", ");
  }
  Serial.println("]%");

  // 조도/스펙트럼 (readSpectrum 이 Spectrum Data 블록을 출력)
  bool specOk = readSpectrum();
  Serial.print("조도: ");
  Serial.println(specOk ? getLightLux() : 0);
  // ── 펌프 테스트: 10초 가동 후 정지 (테스트용, 확인 후 이 블록 삭제) ──
  //Serial.println("펌프 테스트 시작 (10초 가동)");
  //giveWater(10000);   // 10000ms = 10초
  //Serial.println("펌프 테스트 종료 (정지)");

  // ── LED 테스트: 색상 순환 (테스트용, 확인 후 이 블록 삭제) ──
  //Serial.println("LED 테스트: 빨강");
  //setLEDColor(255, 0, 0);     delay(1000);
  //Serial.println("LED 테스트: 초록");
  //setLEDColor(0, 255, 0);     delay(1000);
  //Serial.println("LED 테스트: 파랑");
  //setLEDColor(0, 0, 255);     delay(1000);
  //Serial.println("LED 테스트: 흰색");
  //setLEDColor(255, 255, 255); delay(1000);
  //Serial.println("LED 테스트: 끄기");
  //turnOffLED();               delay(1000);

  // ── 전송 가드 ────────────────────────────────────────────
  if (strlen(cageDeviceId) == 0) {
    Serial.println("DeviceId 미설정 → BLE 초기 설정 필요");
    delay(10000);
    return;
  }

  // ── 서버 전송 (모든 센서값 배열로) ───────────────────────
  String payload = buildJsonData(
    soilValues, NUM_SOIL_SENSORS,
    tempValues, humidValues, NUM_DHT_SENSORS,
    specOk
  );
  sendPostRequest(payload);
  readServerResponse();
  serviceControlCommands();

  waitWithCommandPolling(SENSOR_LOOP_DELAY_MS);
}
