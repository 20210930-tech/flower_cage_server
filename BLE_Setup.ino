// SAMD 계열(MKR WiFi 1010, Nano 33 IoT): FlashStorage_SAMD 라이브러리 필요
// Arduino IDE > 라이브러리 관리자 > "FlashStorage_SAMD" 검색 > 설치 (by Khoi Hoang)
// AVR 계열(Uno, Mega): 기본 EEPROM.h 사용
#if defined(ARDUINO_ARCH_SAMD)
  #include <FlashStorage_SAMD.h>
#else
  #include <EEPROM.h>
#endif

#include <ArduinoBLE.h>

// EEPROM 레이아웃
#define SSID_ADDR        0    // 32 bytes
#define PASS_ADDR        32   // 64 bytes
#define DEVICE_ID_ADDR   96   // 37 bytes (UUID + null)
#define PLANT_ID_ADDR    133  // 37 bytes (UUID + null)

#define SSID_LEN      32
#define PASS_LEN      64
#define UUID_LEN      37

BLEService              wifiService("19B10000-E8F2-537E-4F6C-D104768A1214");
BLEStringCharacteristic wifiDataChar("19B10001-E8F2-537E-4F6C-D104768A1214", BLERead | BLEWrite, 256);

void _eepromWriteStr(int addr, int maxLen, String value) {
  for (int i = 0; i < maxLen; i++) {
    EEPROM.write(addr + i, i < (int)value.length() ? value[i] : 0);
  }
}

void _eepromReadStr(int addr, int maxLen, char* dest) {
  for (int i = 0; i < maxLen; i++) {
    dest[i] = EEPROM.read(addr + i);
  }
}

// EEPROM에 WiFi + 장치 ID 저장
void saveToEEPROM(String newSsid, String newPass,
                  String newDeviceId, String newPlantId) {
  _eepromWriteStr(SSID_ADDR,      SSID_LEN, newSsid);
  _eepromWriteStr(PASS_ADDR,      PASS_LEN, newPass);
  _eepromWriteStr(DEVICE_ID_ADDR, UUID_LEN, newDeviceId);
  _eepromWriteStr(PLANT_ID_ADDR,  UUID_LEN, newPlantId);

  // SAMD 계열은 commit() 호출해야 플래시에 실제 기록됨
  // AVR(Uno/Mega)은 write() 즉시 저장되므로 commit() 불필요
#if defined(ARDUINO_ARCH_SAMD)
  EEPROM.commit();
#endif
}

// 저장된 설정 삭제 (SSID/DeviceId를 비워 다음 부팅 시 BLE 설정 모드로 진입)
void clearWiFiConfig() {
  _eepromWriteStr(SSID_ADDR,      SSID_LEN, "");
  _eepromWriteStr(DEVICE_ID_ADDR, UUID_LEN, "");
#if defined(ARDUINO_ARCH_SAMD)
  EEPROM.commit();
#endif
}

// WiFi 자격증명(SSID/비밀번호)만 지운다. 기기·식물 식별자는 보존한다.
// 잘못된 WiFi 정보로 판단됐을 때 사용한다. 서버 식별자가 유지되므로 재설정 후에도 같은 기기로 인식된다.
void clearWiFiCredsOnly() {
  _eepromWriteStr(SSID_ADDR, SSID_LEN, "");
  _eepromWriteStr(PASS_ADDR, PASS_LEN, "");
#if defined(ARDUINO_ARCH_SAMD)
  EEPROM.commit();
#endif
}

// 내부 저장소에서 와이파이 정보와 식별자를 읽어 온다.
// 저장된 와이파이 정보가 없으면 거짓을 돌려주어, 블루투스 초기 설정으로 가게 한다.
bool loadWiFiFromEEPROM() {
  char loadedSsid[SSID_LEN]    = {0};
  char loadedPass[PASS_LEN]    = {0};
  char loadedDevId[UUID_LEN]   = {0};
  char loadedPlantId[UUID_LEN] = {0};

  _eepromReadStr(SSID_ADDR,      SSID_LEN, loadedSsid);
  _eepromReadStr(PASS_ADDR,      PASS_LEN, loadedPass);
  _eepromReadStr(DEVICE_ID_ADDR, UUID_LEN, loadedDevId);
  _eepromReadStr(PLANT_ID_ADDR,  UUID_LEN, loadedPlantId);

  // 첫 바이트가 영이거나 이백오십오이면 아직 아무것도 저장되지 않은 초기 상태로 본다.
  // 그래서 와이파이 이름이 초기 상태이면 저장된 정보가 없다고 판단한다.
  if (loadedSsid[0] == 0x00 || loadedSsid[0] == 0xFF) return false;

  memcpy(ssid, loadedSsid, SSID_LEN);
  memcpy(pass, loadedPass, PASS_LEN);
  // 식별자가 비어 있어도 블루투스 설정으로 다시 들어가지 않는다.
  // 식별자는 어차피 펌웨어가 만들어 채우므로, 저장된 유효한 값이 있을 때만 읽어 둔다.
  if (loadedDevId[0]   != 0x00 && loadedDevId[0]   != 0xFF) memcpy(cageDeviceId,   loadedDevId,   UUID_LEN);
  if (loadedPlantId[0] != 0x00 && loadedPlantId[0] != 0xFF) memcpy(plantProfileId, loadedPlantId, UUID_LEN);
  return true;
}

// 디바이스 식별자를 펌웨어가 직접 만드는 부분이다.
// 펌웨어가 첫 실행 때 기기 식별자와 식물 식별자를 임의로 생성해 내부 저장소에 보관한다.
// 한 번 만들면 전원 차단이나 재부팅에도 같은 값을 계속 쓰므로, 서버와 식별자가 어긋나지 않는다.
static bool _rndSeeded = false;
static void _seedRandomOnce() {
  if (_rndSeeded) return;
  // 블루투스가 켜진 동안에는 무선 칩 맥 주소를 읽는 함수가 무선 코프로세서와 충돌할 수 있어 쓰지 않는다.
  // 대신 떠 있는 아날로그 핀을 여러 번 읽어 잡음을 누적해, 기기마다 다른 난수 시드를 만든다.
  uint32_t s = micros();
  for (int i = 0; i < 16; i++) {
    s = s * 1103515245u + 12345u;
    s ^= ((uint32_t)analogRead(A1) << 11) ^ ((uint32_t)analogRead(A2) << 5) ^ (uint32_t)analogRead(A3);
    s ^= micros();
  }
  randomSeed(s == 0 ? micros() : s);
  _rndSeeded = true;
}

// 표준 형식의 임의 식별자 문자열을 만든다. 길이는 서른여섯 글자이고 서버의 식별자 형식으로 그대로 읽힌다.
String generateUuidV4() {
  _seedRandomOnce();
  const char* hex = "0123456789abcdef";
  char buf[37];
  int p = 0;
  for (int i = 0; i < 16; i++) {
    byte b = (byte)random(256);
    if (i == 6) b = (b & 0x0F) | 0x40;  // 버전 자리를 표준 형식에 맞게 고정한다
    if (i == 8) b = (b & 0x3F) | 0x80;  // 변형 자리를 표준 형식에 맞게 고정한다
    buf[p++] = hex[b >> 4];
    buf[p++] = hex[b & 0x0F];
    if (i == 3 || i == 5 || i == 7 || i == 9) buf[p++] = '-';
  }
  buf[p] = '\0';
  return String(buf);
}

// 식별자가 비어 있으면 직접 만들어 내부 저장소에 보관한다.
// 이미 값이 있으면 아무 일도 하지 않으므로, 첫 부팅에서만 한 번 생성된다.
void ensureDeviceIds() {
  bool changed = false;
  if (strlen(cageDeviceId) < 36) {
    generateUuidV4().toCharArray(cageDeviceId, UUID_LEN);
    Serial.println("DeviceId 자체 생성: " + String(cageDeviceId));
    changed = true;
  }
  if (strlen(plantProfileId) < 36) {
    generateUuidV4().toCharArray(plantProfileId, UUID_LEN);
    Serial.println("PlantId 자체 생성: " + String(plantProfileId));
    changed = true;
  }
  if (changed) {
    _eepromWriteStr(DEVICE_ID_ADDR, UUID_LEN, String(cageDeviceId));
    _eepromWriteStr(PLANT_ID_ADDR,  UUID_LEN, String(plantProfileId));
#if defined(ARDUINO_ARCH_SAMD)
    EEPROM.commit();
#endif
  }
}

// 블루투스로 와이파이 정보를 받을 때까지 기다린다.
// 식별자는 펌웨어가 소유한다. 앱이 특성을 읽으면 이 기기의 식별자 두 개를 쉼표로 이어 가져간다.
// 앱이 쓰는 값은 와이파이 이름과 비밀번호뿐이며, 식별자는 받지 않는다.
void waitForWiFiInfoViaBLE() {
  if (!BLE.begin()) {
    Serial.println("BLE 모듈 시작 실패!");
    while (1);
  }

  // 광고를 시작하기 전에 식별자를 확보한다. 없으면 여기서 만든다.
  // 그리고 그 식별자를 특성의 초기값으로 실어 두어, 앱이 한 번의 읽기로 이 기기의 식별자를 가져가게 한다.
  ensureDeviceIds();

  BLE.setLocalName("SmartPot_Setup");
  BLE.setAdvertisedService(wifiService);
  wifiService.addCharacteristic(wifiDataChar);
  BLE.addService(wifiService);
  wifiDataChar.writeValue(String(cageDeviceId) + "," + String(plantProfileId));
  BLE.advertise();

  Serial.println("[블루투스 페어링 모드]");
  Serial.println("앱에서 'SmartPot_Setup' 연결 후 WiFi 정보를 전송하세요.");
  Serial.println("이 기기 DeviceId: " + String(cageDeviceId));

  bool received = false;
  while (!received) {
    BLEDevice central = BLE.central();
    if (!central) continue;

    Serial.print("연결됨: ");
    Serial.println(central.address());

    while (central.connected()) {
      if (!wifiDataChar.written()) continue;

      String data = wifiDataChar.value();
      // 형식: "SSID,비밀번호" — deviceId/plantId는 펌웨어가 소유하므로 받지 않는다.
      // (앱은 READ로 이 기기의 ID를 이미 가져갔다. 뒤에 필드가 더 와도 무시한다.)
      int c1 = data.indexOf(',');
      if (c1 <= 0) {
        Serial.println("잘못된 형식. 'SSID,비밀번호' 로 보내주세요.");
        continue;
      }
      int c2 = data.indexOf(',', c1 + 1);  // PASS는 두 번째 필드까지만 사용

      String newSsid = data.substring(0, c1);
      String newPass = (c2 > c1) ? data.substring(c1 + 1, c2) : data.substring(c1 + 1);

      newSsid.toCharArray(ssid, SSID_LEN);
      newPass.toCharArray(pass, PASS_LEN);

      ensureDeviceIds();  // 자체 ID 보장 (이미 생성돼 있으면 no-op)
      saveToEEPROM(String(ssid), String(pass), String(cageDeviceId), String(plantProfileId));

      Serial.println("WiFi 설정 저장 완료! (DeviceId/PlantId는 펌웨어 소유)");
      Serial.println("DeviceId: " + String(cageDeviceId));
      Serial.println("PlantId:  " + String(plantProfileId));

      received = true;
      break;
    }
    Serial.println("연결 끊어짐");
  }

  BLE.stopAdvertise();
  BLE.end();
  Serial.println("블루투스 종료. 와이파이 접속 시도...");
}
