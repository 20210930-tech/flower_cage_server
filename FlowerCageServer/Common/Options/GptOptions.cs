namespace FlowerCageServer.Common.Options;

public class GptOptions
{
    public const string SectionName = "ExternalServices:Gpt";

    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4.1";
    public bool Enabled { get; set; }

    // AI 모드: 이 분(minute)마다 AI에게 상태/센서 기반 제어를 요청한다. (테스트 시 1로)
    public int AiPollIntervalMinutes { get; set; } = 30;
}
