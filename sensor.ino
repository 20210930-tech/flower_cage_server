// ── JSON 배열 빌더 헬퍼 ───────────────────────────────────
static String _intArr(int* arr, int n) {
  String s = "[";
  for (int i = 0; i < n; i++) {
    s += String(arr[i]);
    if (i < n - 1) s += ",";
  }
  return s + "]";
}

static String _floatArr(float* arr, int n) {
  String s = "[";
  for (int i = 0; i < n; i++) {
    s += String(arr[i], 1); // 소수점 1자리
    if (i < n - 1) s += ",";
  }
  return s + "]";
}

// 서버 SensorReadings 규격에 맞춘 JSON 빌드
// 모든 센서값을 배열로 전송 → 서버에서 평균 계산 및 저장
String buildJsonData(int*   soilValues, int numSoil,
                     float* tempValues, float* humidValues, int numDht,
                     bool   specOk) {

  int lightLux = specOk ? getLightLux()     : 0;
  int nm415    = specOk ? getSpectrum415()  : 0;
  int nm445    = specOk ? getSpectrum445()  : 0;
  int nm480    = specOk ? getSpectrum480()  : 0;
  int nm515    = specOk ? getSpectrum515()  : 0;
  int nm555    = specOk ? getSpectrum555()  : 0;
  int nm590    = specOk ? getSpectrum590()  : 0;
  int nm630    = specOk ? getSpectrum630()  : 0;
  int nm680    = specOk ? getSpectrum680()  : 0;
  int clear    = specOk ? getSpectrumClear(): 0;
  int nir      = specOk ? getSpectrumNir()  : 0;

  String json = "{";
  json += "\"cageDeviceId\":\""   + String(cageDeviceId)          + "\",";
  json += "\"plantProfileId\":\"" + String(plantProfileId)        + "\",";

  // 배열로 전송 (서버가 평균 계산)
  json += "\"soilMoistures\":"    + _intArr(soilValues, numSoil)  + ",";
  json += "\"temperatures\":"     + _floatArr(tempValues, numDht) + ",";
  json += "\"humidities\":"       + _floatArr(humidValues, numDht)+ ",";

  // 스펙트럼 (AS7341 단일 센서)
  json += "\"lightLux\":"         + String(lightLux) + ",";
  json += "\"nm415\":"            + String(nm415)    + ",";
  json += "\"nm445\":"            + String(nm445)    + ",";
  json += "\"nm480\":"            + String(nm480)    + ",";
  json += "\"nm515\":"            + String(nm515)    + ",";
  json += "\"nm555\":"            + String(nm555)    + ",";
  json += "\"nm590\":"            + String(nm590)    + ",";
  json += "\"nm630\":"            + String(nm630)    + ",";
  json += "\"nm680\":"            + String(nm680)    + ",";
  json += "\"clear\":"            + String(clear)    + ",";
  json += "\"nir\":"              + String(nir);
  json += "}";
  return json;
}
