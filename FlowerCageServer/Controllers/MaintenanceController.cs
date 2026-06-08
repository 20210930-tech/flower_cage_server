using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FlowerCageServer.Data;

namespace FlowerCageServer.Controllers;

[ApiController]
[Route("api/maintenance")]
public class MaintenanceController : ControllerBase
{
    private readonly FlowerCageDbContext _db;

    public MaintenanceController(FlowerCageDbContext db) => _db = db;

    // 서버 데이터 전체 초기화 (개발/데모용). 모든 테이블의 행을 삭제한다.
    [HttpPost("reset")]
    public async Task<IActionResult> ResetAll(CancellationToken cancellationToken)
    {
        // 로그/자식부터 삭제 (FK는 SetNull이라 순서 무관하나 명시적으로 정리)
        await _db.GptAnalysisLogs.ExecuteDeleteAsync(cancellationToken);
        await _db.GrowthLogs.ExecuteDeleteAsync(cancellationToken);
        await _db.AlertEvents.ExecuteDeleteAsync(cancellationToken);
        await _db.ControlCommands.ExecuteDeleteAsync(cancellationToken);
        await _db.SensorReadings.ExecuteDeleteAsync(cancellationToken);
        await _db.PlantEnvironmentSettings.ExecuteDeleteAsync(cancellationToken);
        await _db.PlantProfiles.ExecuteDeleteAsync(cancellationToken);
        await _db.CageDevices.ExecuteDeleteAsync(cancellationToken);
        await _db.SystemEventLogs.ExecuteDeleteAsync(cancellationToken);
        await _db.Users.ExecuteDeleteAsync(cancellationToken);

        return Ok(new { status = "All server data cleared." });
    }
}
