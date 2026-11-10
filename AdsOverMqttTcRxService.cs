using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TwinCAT.Ads;          // コアクラス (AdsSession, IAdsConnection など)
using TwinCAT.Ads.Reactive; // 拡張メソッド WhenNotification<T> に必要

public class AdsOverMqttTcRxService : BackgroundService
{
    private readonly ILogger<AdsOverMqttTcRxService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _configuration;

    // DIコンテナから ILoggerFactory を受け取るように追加
    public AdsOverMqttTcRxService(
        ILogger<AdsOverMqttTcRxService> logger,
        ILoggerFactory loggerFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ads.Reactive を使用したルーターレス AdsOverMqtt サービスを開始しています...");

        // 1. インプロセスルーター用設定のセット（配列インデックス :0: を含める階層構造）
        _configuration["AmsRouter:Name"] = "MqttRouter";
        _configuration["AmsRouter:NetId"] = "10.10.10.10.1.1";
        _configuration["AmsRouter:ChannelProtocol"] = "Mqtt";
        _configuration["AmsRouter:Mqtt:0:Address"] = "192.168.3.54";
        _configuration["AmsRouter:Mqtt:0:Port"] = "1883";
        _configuration["AmsRouter:Mqtt:0:Topic"] = "AdsOverMqtt";
        _configuration["AmsRouter:Mqtt:0:NetId"] = "15.15.15.15.1.1";

        AmsAddress targetPlcAddress = new AmsAddress("15.15.15.15.1.1", 851);
        SessionSettings settings = SessionSettings.Default;
        // タイムアウトを変更したい場合は以下のように設定可能
        // settings.Timeout = 10000;

        // 2. AdsClient ではなく AdsSession を生成し、_configuration と _loggerFactory を渡す
        using (AdsSession session = new AdsSession(targetPlcAddress, settings, _configuration, _loggerFactory, null))
        {
            try
            {
                // 3. セッションから IAdsConnection を取得して接続
                IAdsConnection connection = (IAdsConnection)session.Connect();
                _logger.LogInformation("PLC ({Address}) に接続成功しました。", targetPlcAddress);

                // 4. 通知の設定定義
                NotificationSettings notificationSettings = new NotificationSettings(
                    AdsTransMode.OnChange,
                    100,
                    0
                );

                // 5. IAdsConnection に対しても WhenNotification<T> がそのまま使用可能
                IObservable<StJobEventMessage> eventMessageObservable =
                    connection.WhenNotification<StJobEventMessage>("demo3.runner.event_message", notificationSettings);

                // 6. Subscribe（購読）した瞬間に、内部で自動的に AddDeviceNotification が実行される
                using (eventMessageObservable
                    .Where(x => x.NewState == "process" || x.OldState == "process")
                    .Subscribe(
                        value =>
                        {
                            _logger.LogInformation("【Rx通知】変更を検知: {EventMessage}", value);
                        },
                        error =>
                        {
                            _logger.LogError(error, "Rxストリーム内でエラーが発生しました。");
                        }))
                {
                    _logger.LogInformation("Device Notification ストリームを購読しました。");

                    // サービスがキャンセルされるまで非同期待機
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }

                _logger.LogInformation("Device Notification ストリームの購読を解除しました。");
            }
            catch (OperationCanceledException)
            {
                // サービス停止時の正常なキャンセル例外は無視
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ADS通信の初期化または実行中にエラーが発生しました。");
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
