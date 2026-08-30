#include <Adafruit_NeoPixel.h>

#define LPIN    13
#define LN_LEDS 8   // 전체 LED 개수. 구역 수(NUM_LED_ZONES)에 맞게 조정

Adafruit_NeoPixel strip = Adafruit_NeoPixel(LN_LEDS, LPIN, NEO_GRB + NEO_KHZ800);

// 현재 전체 색 상태. 측정 중 LED를 잠깐 껐다가(블랭크) 원래 색으로 복원하기 위해 기억한다.
static uint8_t _curR = 0, _curG = 0, _curB = 0;

void setupLED() {
  strip.setBrightness(100);
  strip.begin();
  strip.clear();
  strip.show();
}

// 전체 LED 단색 설정
void setLEDColor(uint8_t r, uint8_t g, uint8_t b) {
  _curR = r; _curG = g; _curB = b;   // 복원용 상태 저장
  for (int i = 0; i < strip.numPixels(); i++) {
    strip.setPixelColor(i, strip.Color(r, g, b));
  }
  strip.show();
}

// ── 조도 측정용 되먹임 차단 ───────────────────────────────
// AS7341이 자기가 켠 LED 빛을 다시 읽어 조도가 왜곡되는 것을 막는다.
// 측정 직전 LED를 끄고(상태는 유지), 측정 후 원래 색으로 되돌린다.
void ledBlankForMeasure() {
  strip.clear();
  strip.show();
}

void ledRestoreAfterMeasure() {
  for (int i = 0; i < strip.numPixels(); i++) {
    strip.setPixelColor(i, strip.Color(_curR, _curG, _curB));
  }
  strip.show();
}

// 특정 구역만 색상 설정 (zone: 0-based, totalZones: 전체 구역 수)
void setLEDZone(int zone, int totalZones, uint8_t r, uint8_t g, uint8_t b) {
  int ledsPerZone = strip.numPixels() / totalZones;
  int start = zone * ledsPerZone;
  int end   = (zone == totalZones - 1) ? strip.numPixels() : start + ledsPerZone;
  for (int i = start; i < end; i++) {
    strip.setPixelColor(i, strip.Color(r, g, b));
  }
  strip.show();
}

// 밝기(0-100%)를 받아 흰색으로 전체 설정
void setLEDBrightness(int percent) {
  uint8_t val = (uint8_t)map(percent, 0, 100, 0, 255);
  setLEDColor(val, val, val);
}

// 밝기(0-100%)를 받아 특정 구역만 설정
void setLEDZoneBrightness(int zone, int totalZones, int percent) {
  uint8_t val = (uint8_t)map(percent, 0, 100, 0, 255);
  setLEDZone(zone, totalZones, val, val, val);
}

void turnOffLED() {
  _curR = 0; _curG = 0; _curB = 0;   // 복원 시에도 꺼진 상태 유지
  strip.clear();
  strip.show();
}

void chaseLED(uint32_t c) {
  for (uint16_t i = 0; i < strip.numPixels() + 4; i++) {
    strip.setPixelColor(i,     c);
    strip.setPixelColor(i - 3, 0);
    strip.show();
    delay(10);
  }
}
