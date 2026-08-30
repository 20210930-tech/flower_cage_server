// soilSensorPins[] 와 NUM_SOIL_SENSORS 는 main.ino 에서 선언

// 모든 토양 수분 센서 raw 값 읽기 → out[i] (0~1023, analogRead 원시값)
// 변환 없이 그대로 전송. 캘리브레이션·퍼센트 계산은 서버에서 처리.
void readAllSoilSensors(int* out) {
  for (int i = 0; i < NUM_SOIL_SENSORS; i++) {
    out[i] = analogRead(soilSensorPins[i]);
  }
}
