using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwinCAT.Ads; // AdsSession, IAdsConnection, AmsAddress, SessionSettings を使用

public class AdsOverMqttService : BackgroundService
{
    private readonly ILogger<AdsOverMqttService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;

    // DIコンテナから ILoggerFactory を受け取るように追加
    public AdsOverMqttService(
        ILogger<AdsOverMqttService> logger,
        ILoggerFactory loggerFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ルーターレス AdsOverMqtt サービスを開始しています...");

        // -------------------------------------------------------------------------
        // 1. コード内（メモリ内）でインプロセスルーターを自動構成するための設定
        //    ※ MqttRouter が認識できる配列インデックス付きの階層構造（:0:）に修正
        // -------------------------------------------------------------------------
        _configuration["AmsRouter:NetId"] = "10.10.10.10.1.1";
        _configuration["AmsRouter:Name"] = "InProcessMqttRouter";
        _configuration["AmsRouter:ChannelProtocol"] = "Mqtt";
        _configuration["AmsRouter:Mqtt:0:Address"] = "192.168.3.54";
        _configuration["AmsRouter:Mqtt:0:Port"] = "1883";
        _configuration["AmsRouter:Mqtt:0:Topic"] = "AdsOverMqtt";
        _configuration["AmsRouter:Mqtt:0:NetId"] = "15.15.15.15.1.1";

        // -------------------------------------------------------------------------
        // 2. AdsSession 用の設定初期化（タイムアウト 10000ms を指定）
        // -------------------------------------------------------------------------
        AmsAddress targetPlcAddress = new AmsAddress("15.15.15.15.1.1", 851);
        SessionSettings settings = SessionSettings.Default;
        settings.Timeout = 10000;

        // -------------------------------------------------------------------------
        // 3. AdsSession の生成と通信（_configuration と _loggerFactory を渡す）
        // -------------------------------------------------------------------------
        using (AdsSession session = new AdsSession(targetPlcAddress, settings, _configuration, _loggerFactory, null))
        {
            try
            {
                // IConnection から IAdsConnection へ明示的にキャストして接続
                IAdsConnection connection = (IAdsConnection)session.Connect();
                _logger.LogInformation("PLC ({Address}) に接続成功しました。", targetPlcAddress);

                // 変数のハンドルを取得（V7 仕様の非同期メソッドも利用可能ですが、元のロジックを維持しています）
                uint handle = 0;
                handle = await Task.Run(() => connection.CreateVariableHandle("demo3.runner.event_message"), stoppingToken);

                try
                {
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        // TwinCAT V7 正式仕様: 型引数を指定したジェネリック版
                        ResultValue<StJobEventMessage> readResult = await connection.ReadAnyAsync<StJobEventMessage>(handle, stoppingToken);

                        if (readResult.Succeeded)
                        {
                            StJobEventMessage value = readResult.Value;
                            _logger.LogInformation("値: {Value}", value.ToString());
                        }
                        else
                        {
                            _logger.LogWarning("データの読み込みに失敗しました。ErrorCode: {ErrorCode}", readResult.ErrorCode);
                        }

                        await Task.Delay(1000, stoppingToken);
                    }
                }
                finally
                {
                    if (handle != 0)
                    {
                        await Task.Run(() => connection.DeleteVariableHandle(handle), CancellationToken.None);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // サービス停止時の正常なキャンセル例外は無視
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ADS通信中にエラーが発生しました。");
            }
            finally
            {
                if (session.IsConnected)
                {
                    session.Disconnect();
                    _logger.LogInformation("PLC との接続を切断しました。");
                }
            }
        }
    }
}
