namespace FlowerCageServer.Services.Fuzzy;

// 퍼지 멤버십 함수 모음이다. 모든 함수는 영부터 일 사이의 소속도를 반환한다.
// 사다리꼴 대신 어깨 모양 함수와 삼각형 함수를 조합해 언어 변수를 표현한다.
public static class MembershipFunctions
{
    // 삼각형 멤버십 함수이다. 왼쪽 끝 a에서 소속도가 영이고, 꼭대기 b에서 일이며,
    // 오른쪽 끝 c에서 다시 영으로 내려간다. 그 사이는 직선으로 잇는다.
    public static double Triangular(double x, double a, double b, double c)
    {
        if (x <= a || x >= c) return 0;
        if (x <= b) return b <= a ? 1 : (x - a) / (b - a);
        return c <= b ? 1 : (c - x) / (c - b);
    }

    // 왼쪽 어깨 함수이다. x가 a 이하이면 소속도가 일이고, b 이상이면 영이며,
    // 그 사이에서는 직선으로 내려간다.
    public static double LeftShoulder(double x, double a, double b)
    {
        if (x <= a) return 1;
        if (x >= b) return 0;
        return (b - x) / (b - a);
    }

    // 오른쪽 어깨 함수이다. x가 a 이하이면 소속도가 영이고, b 이상이면 일이며,
    // 그 사이에서는 직선으로 올라간다.
    public static double RightShoulder(double x, double a, double b)
    {
        if (x <= a) return 0;
        if (x >= b) return 1;
        return (x - a) / (b - a);
    }
}

// Mamdani 추론의 출력 변수를 표현한다.
// 발화된 각 규칙의 결론 집합을 규칙 강도만큼 잘라서, 출력 구간 위에 가장 큰 값으로 누적한다.
// 모든 규칙을 누적한 뒤 무게중심을 구해 하나의 출력값으로 역퍼지화한다.
public sealed class FuzzyOutput
{
    private readonly double _min;
    private readonly double _max;
    private readonly int _steps;
    private readonly double[] _aggregate;

    public FuzzyOutput(double min, double max, int steps = 101)
    {
        _min = min;
        _max = max;
        _steps = steps < 2 ? 2 : steps;
        _aggregate = new double[_steps];
    }

    // 규칙 하나를 발화한다. 규칙 강도만큼 결론 멤버십 함수를 잘라서 출력 구간에 누적한다.
    // 같은 지점에 여러 규칙이 겹치면 더 큰 값을 남긴다.
    public void Apply(double strength, Func<double, double> term)
    {
        if (strength <= 0) return;
        for (var i = 0; i < _steps; i++)
        {
            var x = _min + (_max - _min) * i / (_steps - 1);
            var clipped = Math.Min(strength, term(x));
            if (clipped > _aggregate[i]) _aggregate[i] = clipped;
        }
    }

    // 누적된 출력 모양의 무게중심을 구해 하나의 출력값으로 바꾼다.
    // 발화된 규칙이 하나도 없으면 영을 반환한다.
    public double Defuzzify()
    {
        double numerator = 0, denominator = 0;
        for (var i = 0; i < _steps; i++)
        {
            var x = _min + (_max - _min) * i / (_steps - 1);
            numerator += x * _aggregate[i];
            denominator += _aggregate[i];
        }
        return denominator < 1e-9 ? 0 : numerator / denominator;
    }
}
