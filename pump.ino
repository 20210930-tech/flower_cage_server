int pumpIN1 = 4;
int pumpIN2 = 5;

void setupPump() {
  pinMode(pumpIN1, OUTPUT);
  pinMode(pumpIN2, OUTPUT);
  digitalWrite(pumpIN1, LOW);
  digitalWrite(pumpIN2, LOW);
}

void giveWater(int tick) {  // tick 기준 1000 = 1초
  Serial.println("물 주기 시작! (펌프 가동)");
  digitalWrite(pumpIN1, HIGH);
  digitalWrite(pumpIN2, LOW);
  delay(tick);
  Serial.println("물 주기 완료! (펌프 정지)");
  digitalWrite(pumpIN1, LOW);
  digitalWrite(pumpIN2, LOW);
}
