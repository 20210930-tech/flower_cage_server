// 펌프 ml/초 변환 상수 (실제 펌프로 캘리브레이션 후 수정)
#define PUMP_ML_PER_SEC 50.0

// 한 번에 시도할 연결 횟수. 이 횟수만큼 실패하면 한 묶음(사이클)의 실패로 본다. (필요 시 조정)
#define WIFI_MAX_RETRIES 5
// 이전에 연결된 적 있는 기기가 이 횟수만큼의 '재시도 묶음'을 연속 실패하면(일시 끊김치고는 너무 김)
// 블루투스 설정 모드로 빠져 사용자가 새 WiFi를 넣을 수 있게 한다. (자격증명은 지우지 않음)
// 한 묶음 ≈ 5회×5초 = 25초 → 6묶음 ≈ 2.5분. 보통 공유기 재부팅은 이 안에 복구된다.
#define WIFI_BLE_FALLBACK_CYCLES 6

// 와이파이 연결.
//  - 한 번도 연결된 적 없는데 한 묶음(WIFI_MAX_RETRIES 회) 실패: 저장된 WiFi 정보가 틀렸다고 보고
//    그 정보를 지운 뒤(기기 식별자는 보존) 곧바로 블루투스 설정 모드로 전환해 새 정보를 받는다.
//  - 이전에 연결된 적 있으면(일시적 공유기/신호 문제): 자격증명을 지우지 않고 계속 재시도한다.
//    다만 WIFI_BLE_FALLBACK_CYCLES 묶음(≈2.5분) 넘게 계속 실패하면(비밀번호가 영구히 바뀐 경우 등)
//    자격증명은 보존한 채 블루투스 설정 모드로 빠져, 무한 재시도로 갇히지 않게 탈출구를 둔다.
void connectToWiFi() {
  if (WiFi.status() == WL_NO_MODULE) {
    Serial.println("WiFi 모듈 오류!");
    while (true);
  }

  int failedCycles = 0;
  while (WiFi.status() != WL_CONNECTED) {
    int retryCount = 0;
    while (WiFi.status() != WL_CONNECTED && retryCount < WIFI_MAX_RETRIES) {
      Serial.print("연결 시도 중 (");
      Serial.print(retryCount + 1);
      Serial.print("/");
      Serial.print(WIFI_MAX_RETRIES);
      Serial.println(")...");
      status = WiFi.begin(ssid, pass);
      delay(5000);
      retryCount++;
    }

    if (WiFi.status() == WL_CONNECTED) break;
    failedCycles++;

    if (!wifiEverConnected) {
      // 처음부터 연결 실패 → 저장된 WiFi 정보가 틀렸을 가능성이 큼. 정보를 초기화 후 재설정 모드로.
      Serial.print("WiFi 연결 실패 (");
      Serial.print(WIFI_MAX_RETRIES);
      Serial.println("회) → 저장된 WiFi 정보 초기화 후 블루투스 설정 모드");
      clearWiFiCredsOnly();      // SSID/비밀번호만 지움 (기기·식물 식별자는 보존)
      waitForWiFiInfoViaBLE();   // 앱에서 새 WiFi 정보를 받을 때까지 대기
      failedCycles = 0;
    } else if (failedCycles >= WIFI_BLE_FALLBACK_CYCLES) {
      // 이전엔 됐는데 너무 오래 실패 → 비밀번호 변경 등으로 보고 설정 모드로 탈출구 제공.
      // 자격증명은 지우지 않으므로, 사용자가 손대지 않고 재부팅하면 기존 정보로 다시 시도한다.
      Serial.println("WiFi 장시간 실패 → 블루투스 설정 모드로 전환 (자격증명은 보존)");
      waitForWiFiInfoViaBLE();
      failedCycles = 0;
    } else {
      // 짧은 일시 끊김 → 자격증명 유지하고 계속 재시도 (공유기 복구 시 자동 연결)
      Serial.print("WiFi 일시 끊김 (묶음 ");
      Serial.print(failedCycles);
      Serial.print("/");
      Serial.print(WIFI_BLE_FALLBACK_CYCLES);
      Serial.println(") → 자격증명 유지하고 재시도 계속");
    }
  }

  wifiEverConnected = true;
  Serial.println("WiFi 연결 성공!");
  Serial.print("IP: ");
  Serial.println(WiFi.localIP());
}

// 와이파이 연결 상태를 점검해 끊겼으면 다시 연결한다. 메인 루프에서 매 주기 호출한다.
void ensureWiFiConnected() {
  if (WiFi.status() == WL_CONNECTED) return;

  Serial.println("WiFi 끊김 감지 → 재연결 시도");
  if (client.connected()) client.stop();   // 죽은 소켓 정리
  WiFi.disconnect();
  connectToWiFi();                          // 내부에서 최대 5회 재시도
}

// 서버에 TCP로 연결하되, 실패하면 다시 시도한다.
// 무선 칩은 일시적으로 연결이 실패하는 일이 잦으므로, 한 번 실패했다고 그 주기를 통째로 버리지 않고
// 짧게 여러 번 시도한다. 이렇게 해야 일시적 실패로 인한 데이터 유실을 줄일 수 있다.
// 시도 사이의 대기 시간은 시도가 거듭될수록 점점 길어진다.
bool connectWithRetry(int tries) {
  for (int i = 0; i < tries; i++) {
    if (client.connected()) client.stop();   // 죽은 소켓을 정리한 뒤 새로 연결한다
    if (client.connect(server, port)) return true;

    Serial.print("[NET] 서버 connect 실패 (");
    Serial.print(i + 1); Serial.print("/"); Serial.print(tries);
    Serial.println(")");

    ensureWiFiConnected();        // 실패 원인이 와이파이 끊김이면 먼저 복구한다. 연결돼 있으면 바로 돌아온다.
    delay(300L * (i + 1));        // 시도가 거듭될수록 삼백, 육백, 구백 밀리초로 대기 시간을 늘린다
  }
  return false;
}

// HTTP 응답에서 바디만 추출
String _readBody() {
  Serial.println("[CMD] READ START");

  String full = "";
  full.reserve(2048);  // 글자 단위 += 재할당을 줄여 힙 조각화/메모리 부족 방지 (SAMD)
  unsigned long lastActivity = millis();

  while ((client.connected() || client.available()) &&
         millis() - lastActivity < 5000) {

    while (client.available()) {
      full += (char)client.read();
      lastActivity = millis();
    }

    delay(1);
  }

  Serial.println("[CMD] READ END");

  client.stop();

  int bodyStart = full.indexOf("\r\n\r\n");
  return (bodyStart >= 0) ? full.substring(bodyStart + 4) : "";
}

// 센서 데이터 전송 (POST /api/sensor-readings)
void sendPostRequest(String jsonData) {
  Serial.println("서버로 데이터 전송 시도...");

  if (!connectWithRetry(3)) {
    Serial.println("서버 연결 실패! (재시도 3회 초과) — 이번 주기 전송 건너뜀");
    return;
  }

  client.println("POST /api/sensor-readings HTTP/1.0");
  client.print("Host: "); client.println(server);
  client.println("Content-Type: application/json");
  client.print("Content-Length: "); client.println(jsonData.length());
  client.println("Connection: close");
  client.println();
  client.println(jsonData);

  Serial.println("전송 완료: " + jsonData);
}

// POST 응답 확인
void readServerResponse() {
  int timeout = 0;
  while (!client.available() && timeout < 5000) {
    delay(10); timeout += 10;
  }

  if (client.available()) {
    Serial.println("서버 응답:");
    while (client.available()) {
      Serial.print((char)client.read());
    }
    Serial.println();
  } else {
    Serial.println("서버 응답 없음 (타임아웃).");
  }

  client.stop();
  Serial.println("-----------------------------");
}

String getControlCommands() {
  if (!connectWithRetry(3)) {
    Serial.println("[CMD] 명령 조회 연결 실패 (재시도 3회 초과)");
    return "";
  }

  // status=Pending 으로 서버가 미처리 명령만 내려주도록 제한 (전체 이력 다운로드 방지).
  // take=10 으로 응답 크기를 묶어 SAMD(32KB RAM)에서 메모리 폭증/멈춤을 막는다.
  String path = "/api/control-commands?cageDeviceId=";
  path += String(cageDeviceId);
  path += "&status=Pending&take=10";

  Serial.println("[CMD] GET " + path);
  client.println("GET " + path + " HTTP/1.0");
  client.print("Host: "); client.println(server);
  client.println("Accept: application/json");
  client.println("Connection: close");
  client.println();

  String body = _readBody();
  Serial.print("[CMD] 응답 길이: ");
  Serial.println(body.length());
  if (body.length() > 0) {
    Serial.print("[CMD] 응답 본문: ");
    Serial.println(body.length() > 300 ? body.substring(0, 300) + "..." : body);
  }
  return body;
}

// 명령 실행 완료 처리 (PATCH /api/control-commands/{id}/status)
void markCommandApplied(String commandId) {
  if (!connectWithRetry(3)) {
    Serial.println("[CMD] Applied 보고 연결 실패 (재시도 3회 초과): " + commandId);
    return;
  }

  String body = "{\"status\":\"Applied\"}";
  String path = "/api/control-commands/" + commandId + "/status";

  Serial.println("[CMD] PATCH " + path + " -> Applied");
  client.println("PATCH " + path + " HTTP/1.0");
  client.print("Host: "); client.println(server);
  client.println("Content-Type: application/json");
  client.print("Content-Length: "); client.println(body.length());
  client.println("Connection: close");
  client.println();
  client.println(body);

  String response = _readBody(); // 응답 소비
  Serial.print("[CMD] Applied 보고 응답 길이: ");
  Serial.println(response.length());
}

// JSON 문자열 필드 추출
String _extractStr(String obj, String key) {
  String needle = "\"" + key + "\":\"";
  int start = obj.indexOf(needle);
  if (start < 0) return "";
  start += needle.length();
  int end = obj.indexOf("\"", start);
  return (end > start) ? obj.substring(start, end) : "";
}

// JSON 숫자 필드 추출
float _extractFloat(String obj, String key, float defaultVal) {
  String needle = "\"" + key + "\":";
  int start = obj.indexOf(needle);
  if (start < 0) return defaultVal;
  start += needle.length();
  int end = start;
  while (end < (int)obj.length() && obj[end] != ',' && obj[end] != '}') end++;
  String numStr = obj.substring(start, end);
  numStr.trim();
  return numStr.length() > 0 ? numStr.toFloat() : defaultVal;
}

// 응답에서 Pending 명령 찾아 실행
int executePendingCommands(String response) {
  int searchFrom = 0;
  int executed = 0;
  while (true) {
    int pendingPos = response.indexOf("\"Pending\"", searchFrom);
    if (pendingPos < 0) break;

    int objStart = response.lastIndexOf('{', pendingPos);
    int objEnd   = response.indexOf('}', pendingPos);
    if (objStart < 0 || objEnd < 0) break;

    String obj     = response.substring(objStart, objEnd + 1);
    String cmdId   = _extractStr(obj, "id");
    String cmdType = _extractStr(obj, "commandType");

    Serial.println("[CMD] Pending 명령 수신: " + cmdType + " (" + cmdId + ")");

    if (cmdType == "Water") {
      // 급수 시간이 초 단위로 오면 그 값을 먼저 쓴다. 없으면 밀리리터 값을 펌프 유량으로 나눠 초로 환산한다.
      float sec = _extractFloat(obj, "waterDurationSec", -1);
      int tick;
      if (sec >= 0) {
        tick = (int)(sec * 1000);
      } else {
        float ml = _extractFloat(obj, "waterAmountMl", 3.0);
        tick = (int)(ml / PUMP_ML_PER_SEC * 1000);
      }
      Serial.print("[CMD] 급수 실행(ms): ");
      Serial.println(tick);
      giveWater(tick);
      markCommandApplied(cmdId);
      executed++;

    } else if (cmdType == "Led") {
      // 적녹청 값이 오면 그 값을 먼저 쓴다. 없으면 밝기 퍼센트를 흰색 값으로 환산한다.
      int r = (int)_extractFloat(obj, "ledR", -1);
      int g = (int)_extractFloat(obj, "ledG", -1);
      int b = (int)_extractFloat(obj, "ledB", -1);
      if (r >= 0 && g >= 0 && b >= 0) {
        Serial.print("[CMD] LED RGB 실행: ");
        Serial.print(r); Serial.print(",");
        Serial.print(g); Serial.print(",");
        Serial.println(b);
        setLEDColor((uint8_t)r, (uint8_t)g, (uint8_t)b);
      } else {
        int brightness = (int)_extractFloat(obj, "ledBrightness", 50);
        uint8_t val = (uint8_t)map(brightness, 0, 100, 0, 255);
        Serial.print("[CMD] LED 밝기 실행(%): ");
        Serial.println(brightness);
        setLEDColor(val, val, val);
      }
      markCommandApplied(cmdId);
      executed++;

    } else if (cmdType == "AutoMode") {
      // 자동 모드 여부는 서버가 관리하므로 여기서는 완료 처리만 한다.
      Serial.println("[CMD] AutoMode 완료 처리");
      markCommandApplied(cmdId);
      executed++;

    } else if (cmdType == "ResetConfig") {
      // 기기 초기화 명령이다. 완료를 보고한 뒤 저장된 설정을 지우고 재부팅해 블루투스 설정 모드로 들어간다.
      markCommandApplied(cmdId);
      Serial.println("[CMD] 기기 초기화 명령 수신 → 설정 삭제 후 재부팅");
      clearWiFiConfig();
      delay(500);
#if defined(ARDUINO_ARCH_SAMD)
      NVIC_SystemReset();
#else
      // 비-SAMD: 와치독/소프트리셋 대체 — 무한루프로 재부팅 유도
      while (true);
#endif
    } else {
      Serial.println("[CMD] 지원하지 않는 명령 타입: " + cmdType);
    }

    searchFrom = objEnd + 1;
  }

  if (executed == 0) {
    Serial.println("[CMD] Pending 명령 없음");
  }
  return executed;
}

// 제어 명령 폴링 (loop에서 주기적 호출)
void pollControlCommands() {
  if (strlen(cageDeviceId) == 0) {
    Serial.println("[CMD] DeviceId 미설정 → 명령 조회 스킵");
    return;
  }

  String response = getControlCommands();
  if (response.length() == 0) {
    Serial.println("[CMD] 명령 응답 없음");
    return;
  }

  executePendingCommands(response);
}

// 센서 전송 루프와 별도 주기로 명령을 확인한다. 실제 스레드는 아니지만 loop 지연과 분리된다.
void serviceControlCommands() {
  static unsigned long lastPollAt = 0;
  unsigned long now = millis();
  if (lastPollAt != 0 && now - lastPollAt < COMMAND_POLL_INTERVAL_MS) return;

  lastPollAt = now;
  pollControlCommands();
}

void waitWithCommandPolling(unsigned long waitMs) {
  unsigned long start = millis();
  while (millis() - start < waitMs) {
    serviceControlCommands();
    delay(100);
  }
}
