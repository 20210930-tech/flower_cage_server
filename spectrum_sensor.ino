#include <Wire.h>
#include <Adafruit_AS7341.h>

Adafruit_AS7341 as7341;

static uint16_t _specReadings[12];
static bool _specInited = false;

// ── 조도(lux) 보정 계수 ────────────────────────────────────
// AS7341은 lux를 직접 주지 않으므로 Clear 채널 raw 카운트에 계수를 곱해 근사한다.
// 보정 방법: 스마트폰 조도 측정 앱 등 기준값(lux) ÷ 시리얼에 찍히는 Clear raw 값.
//   예) 기준 8000 lux일 때 Clear가 16000이면 → 8000/16000 = 0.5
// 아래 값은 임시 기본값. 실제 환경에서 한 번 보정해 바꾸세요.
#define LUX_PER_CLEAR_COUNT  0.5f

// 센서 초기화 시도 (성공 시 true). 부팅·복구 양쪽에서 호출
static bool initSpectrum() {
  if (!as7341.begin()) {
    return false;
  }
  // 게인을 낮춰 실내+자체 LED 환경에서 Clear 채널이 65535로 포화되는 것을 막는다.
  // (포화되면 항상 "매우 밝음"으로 읽혀 자동제어가 LED를 꺼버림)
  // 적분 시간도 줄여 측정 중 LED 블랭크(꺼짐) 시간을 짧게 한다.
  as7341.setATIME(50);
  as7341.setASTEP(599);            // 적분 ≈ (50+1)*(599+1)*2.78µs ≈ 85ms
  as7341.setGain(AS7341_GAIN_64X); // 포화되면 더 낮추고(32X/16X), 너무 어두우면 올리세요
  return true;
}

void setupSpectrum() {
  Wire.begin();
  Wire.setClock(100000);   // I2C 100kHz로 낮춤 → 긴 선/노이즈에 강해짐 (간헐 끊김 완화)
  _specInited = initSpectrum();
  if (!_specInited) {
    // 못 찾아도 멈추지 않음 → 나머지 센서는 계속 동작, loop에서 재시도
    Serial.println("AS7341 not found (부팅 시) → loop에서 재연결 시도");
  }
}

// 읽기 성공 시 true, 실패 시 false
bool readSpectrum() {
  // 초기화 안 됐으면(부팅 실패/연결 끊김) 재연결 시도
  if (!_specInited) {
    _specInited = initSpectrum();
    if (!_specInited) {
      Serial.println("AS7341 재연결 실패 → I2C 배선(SDA/SCL/전원) 확인");
      return false;
    }
    Serial.println("AS7341 재연결 성공");
  }

  // LED 되먹임 차단: 측정 동안 LED를 잠깐 끄고, 끝나면 원래 색으로 복원.
  // (자체 LED 빛이 조도에 섞이면 켜짐↔꺼짐 진동이 생김)
  ledBlankForMeasure();
  delay(5);  // 잔광/센서 안정화 대기

  // 한 번 실패해도 즉시 몇 번 재시도 (간헐적 I2C 끊김 흡수)
  bool ok = false;
  for (int attempt = 0; attempt < 3; attempt++) {
    if (as7341.readAllChannels(_specReadings)) { ok = true; break; }
    delay(20);
  }

  ledRestoreAfterMeasure();

  if (!ok) {
    Serial.println("AS7341 Read error → 다음 주기에 재초기화 시도");
    _specInited = false;  // 다음 호출 때 begin부터 다시
    return false;
  }

  Serial.println("===== Spectrum Data =====");
  Serial.print("415nm Violet : "); Serial.println(_specReadings[0]);
  Serial.print("445nm Indigo : "); Serial.println(_specReadings[1]);
  Serial.print("480nm Blue   : "); Serial.println(_specReadings[2]);
  Serial.print("515nm Cyan   : "); Serial.println(_specReadings[3]);
  Serial.print("555nm Green  : "); Serial.println(_specReadings[6]);
  Serial.print("590nm Yellow : "); Serial.println(_specReadings[7]);
  Serial.print("630nm Orange : "); Serial.println(_specReadings[8]);
  Serial.print("680nm Red    : "); Serial.println(_specReadings[9]);
  Serial.print("Clear        : "); Serial.println(_specReadings[10]);
  Serial.print("NIR          : "); Serial.println(_specReadings[11]);
  Serial.println();

  return true;
}

// 채널별 개별 getter
int getSpectrum415()  { return (int)_specReadings[0];  } // Violet
int getSpectrum445()  { return (int)_specReadings[1];  } // Indigo
int getSpectrum480()  { return (int)_specReadings[2];  } // Blue
int getSpectrum515()  { return (int)_specReadings[3];  } // Cyan
int getSpectrum555()  { return (int)_specReadings[6];  } // Green
int getSpectrum590()  { return (int)_specReadings[7];  } // Yellow
int getSpectrum630()  { return (int)_specReadings[8];  } // Orange
int getSpectrum680()  { return (int)_specReadings[9];  } // Red
int getSpectrumClear(){ return (int)_specReadings[10]; } // Clear (광대역)
int getSpectrumNir()  { return (int)_specReadings[11]; } // NIR

// Clear 채널 raw 카운트를 보정 계수로 lux 근사값으로 변환.
// (raw 자체는 getSpectrumClear()로 따로 전송되므로 스펙트럼 표시는 영향 없음)
int getLightLux() { return (int)(_specReadings[10] * LUX_PER_CLEAR_COUNT); }
