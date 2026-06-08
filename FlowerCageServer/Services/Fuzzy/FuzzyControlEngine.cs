using static FlowerCageServer.Services.Fuzzy.MembershipFunctions;

namespace FlowerCageServer.Services.Fuzzy;

// 자동 모드의 퍼지 추론 한 번의 결과를 담는 자료형이다.
// Value 는 역퍼지화로 얻은 출력값이며 조명에서는 LED 밝기 퍼센트를 뜻한다.
// Act 는 실제 제어 명령을 낼 필요가 있는지를 나타내며 목표 범위 안이면 거짓이다.
// Trace 는 퍼지화와 규칙 발화와 역퍼지화 과정을 사람이 읽을 수 있게 적은 설명으로
// 데모나 로그에서 추론 근거를 확인하는 데 쓴다.
public sealed record FuzzyDecision(double Value, bool Act, List<string> Trace);

// 식물 환경 자동 제어를 위한 Mamdani 방식 퍼지 추론 엔진이다.
// 모든 입력은 식물별 목표 범위인 최솟값과 최댓값을 기준으로 정규화하므로,
// 센서 단위가 퍼센트이든 원시값이든 상관없이 동작한다.
// 정규화 값은 목표 중앙에서 측정값을 뺀 뒤 범위 절반으로 나눈 값으로,
// 양수일수록 값이 부족하고 음수일수록 값이 과다함을 뜻한다.
// 이 값은 마이너스 이부터 플러스 이까지로 제한하므로, 목표 경계에서 절댓값 일이 되고
// 그보다 더 벗어나면 절댓값 이로 포화된다.
public static class FuzzyControlEngine
{
    // 조명 퍼지 제어기이다. 입력은 조도 한 개이고 출력은 영부터 백까지의 LED 밝기 퍼센트이다.
    // 주변광이 어두울수록 LED 밝기를 올리고, 너무 밝으면 LED를 낮추는 방향으로 추론한다.
    public static FuzzyDecision EvaluateLighting(decimal lux, decimal lightMin, decimal lightMax)
    {
        var trace = new List<string>();

        // 정규화 값이 양수일수록 주변광이 어둡다는 뜻이다.
        var dk = Normalize(lux, lightMin, lightMax);

        // 조도 입력을 네 가지 언어 변수로 퍼지화한다.
        // 차례대로 밝음과 적정과 어두움과 매우 어두움에 대한 소속도이다.
        var bright   = LeftShoulder(dk, -1, 0);
        var okLight  = Triangular(dk, -1, 0, 1);
        var dim      = Triangular(dk, 0, 1, 2);
        var dark     = RightShoulder(dk, 1, 2);

        trace.Add($"퍼지화[조도] 밝음 {bright:F2} / 적정 {okLight:F2} / 어두움 {dim:F2} / 매우어두움 {dark:F2}  (정규화 dk={dk:F2})");

        // 출력 쪽 언어 변수의 멤버십 함수이다. 영부터 백까지의 LED 밝기 퍼센트 위에 정의한다.
        double VeryLow(double b) => LeftShoulder(b, 0, 20);
        double Low(double b)     => Triangular(b, 10, 30, 50);
        double Mid(double b)     => Triangular(b, 40, 60, 80);
        double High(double b)    => RightShoulder(b, 75, 100);

        // 규칙 발화이다. 입력 소속도만큼 결론 집합을 잘라 출력에 누적한다.
        // 너무 밝으면 밝기를 최소로, 적정이면 약하게 보조, 어두우면 중간, 매우 어두우면 최대로 둔다.
        var output = new FuzzyOutput(0, 100);
        output.Apply(bright, VeryLow);
        output.Apply(okLight, Low);
        output.Apply(dim, Mid);
        output.Apply(dark, High);

        // 역퍼지화로 밝기를 구하고 오 퍼센트 단위로 반올림한다.
        var brightness = Math.Round(output.Defuzzify() / 5.0) * 5.0;
        // 주변광이 적정이라 적정 소속도가 가장 우세하면 굳이 LED를 건드리지 않는다.
        var act = Math.Max(Math.Max(bright, dim), dark) > okLight + 1e-3;
        trace.Add($"역퍼지화[조명] LED 밝기 {brightness:F0}% (명령 {(act ? "생성" : "불필요")})");

        return new FuzzyDecision(brightness, act, trace);
    }

    // 측정값을 목표 범위 기준으로 마이너스 이부터 플러스 이까지의 값으로 정규화한다.
    // 결과가 양수일수록 측정값이 목표보다 부족하다는 뜻이다.
    private static double Normalize(decimal value, decimal min, decimal max)
    {
        var center = ((double)min + (double)max) / 2.0;
        var half = Math.Max(1e-4, ((double)max - (double)min) / 2.0);
        return Math.Clamp((center - (double)value) / half, -2, 2);
    }
}
