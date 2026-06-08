using Microsoft.EntityFrameworkCore;
using FlowerCageServer.Common.Options;
using FlowerCageServer.Data;
using FlowerCageServer.Repositories;
using FlowerCageServer.Services;

namespace FlowerCageServer.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GptOptions>(configuration.GetSection(GptOptions.SectionName));
        services.Configure<IotOptions>(configuration.GetSection(IotOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        services.AddDbContext<FlowerCageDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ISystemEventLogService, SystemEventLogService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICageDeviceService, CageDeviceService>();
        services.AddScoped<IPlantProfileService, PlantProfileService>();
        services.AddScoped<IPlantEnvironmentSettingService, PlantEnvironmentSettingService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<ISensorReadingService, SensorReadingService>();
        services.AddScoped<IGrowthLogService, GrowthLogService>();
        services.AddScoped<IControlCommandService, ControlCommandService>();
        services.AddScoped<IControlScheduleService, ControlScheduleService>();
        // 카메라 릴레이: 기기별 최신 프레임 메모리 보관 (싱글톤)
        services.AddSingleton<CameraFrameStore>();
        // GptAnalysisService는 HttpClient + 여러 리포지토리 + ControlCommand 필요
        services.AddHttpClient<IGptAnalysisService, GptAnalysisService>();
        services.AddScoped<IAutoControlService, AutoControlService>();

        // AI 모드: 주기적으로 AI에게 제어를 요청하는 백그라운드 서비스
        services.AddHostedService<AiControlBackgroundService>();
        // 시간 예약 실행 백그라운드 서비스
        services.AddHostedService<ScheduleBackgroundService>();

        return services;
    }

    public static IServiceCollection AddOpenApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi();

        return services;
    }
}
