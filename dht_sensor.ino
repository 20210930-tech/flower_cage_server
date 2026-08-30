#include <DHT.h>

#define DHTTYPE DHT11

// ── 핀 설정 ────────────────────────────────────────────
#define DHTPIN1 2
#define DHTPIN2 3   // NUM_DHT_SENSORS >= 2 일 때 사용
// ──────────────────────────────────────────────────────

DHT _dht1(DHTPIN1, DHTTYPE);

#if NUM_DHT_SENSORS >= 2
DHT _dht2(DHTPIN2, DHTTYPE);
#endif

void setupDHT() {
  _dht1.begin();
#if NUM_DHT_SENSORS >= 2
  _dht2.begin();
#endif
}

// 모든 온도 센서 개별 읽기 → out[i] (°C)
// 평균은 서버에서 계산
void readAllTemperatures(float* out) {
  out[0] = _dht1.readTemperature();
#if NUM_DHT_SENSORS >= 2
  out[1] = _dht2.readTemperature();
#endif
}

// 모든 습도 센서 개별 읽기 → out[i] (%)
// 평균은 서버에서 계산
void readAllHumidities(float* out) {
  out[0] = _dht1.readHumidity();
#if NUM_DHT_SENSORS >= 2
  out[1] = _dht2.readHumidity();
#endif
}
