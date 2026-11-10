# AdsOverMqttNetSample

Beckhoff TwinCAT 3 ランタイム（XAR）へ **ADS over MQTT** プロトコル経由で接続し、PLC 上の構造体データを収集する .NET 10 バックグラウンドサービスのサンプルプロジェクトです。

ローカルに TwinCAT Router（システムサービス）をインストール・常駐させることなく、アプリケーション内部の構成のみで通信を行う **ルーターレス構成（インプロセスルーター）** を採用しているため、Linux 環境や Docker コンテナ上でもそのまま動作します。

---

## 主な特徴

* **ルーターレス構成（In-Process Router）**: `IConfiguration` を用いてインプロセスでルーターを構成し、`AdsSession` に渡すことで、外部の TwinCAT Router なしで MQTT ブローカー経由の ADS 通信を実現します。
* **2つのデータ収集パターンを実装**:
  * **イベント駆動型（Reactive Extensions）**: `AdsOverMqttTcRxService` により、PLC 側の値変化（`OnChange`）をリアクティブストリームで購読・フィルタリングします（デフォルト有効）。
  * **定期ポーリング型**: `AdsOverMqttService` により、変数ハンドルを用いて1秒周期でデータを読み出します。
* **厳密な構造体マーシャリング**: PLC のメモリレイアウト（1バイトアライメント）と一致させた `StJobEventMessage` および `NcToPlcAxisRef` のマッピング定義を含みます。

---

## プロジェクト構成

```text
AdsOverMqttNetSample/
├── AdsOverMqttNetSample.csproj   # プロジェクト定義・NuGetパッケージ構成 (.NET 10)
├── Program.cs                    # Webアプリケーションおよびバックグラウンドサービスのエントリーポイント
├── AdsOverMqttTcRxService.cs     # Rx (WhenNotification) を用いた値変化通知サービス（デフォルト有効）
├── AdsOverMqttService.cs         # 1秒周期のポーリングによる変数読み取りサービス
├── TwinCATStructure.cs           # PLC側構造体（StJobEventMessage / NcToPlcAxisRef）のC#マッピング定義
├── appsettings.json              # アプリケーション・ログ・AmsRouter設定ファイル
└── Properties/
    └── launchSettings.json       # 開発環境向けの起動プロファイル・ポート設定
```

---

## 動作要件

* **.NET SDK**: .NET 10.0 (`net10.0`)
* **MQTT ブローカー**: Mosquitto などの標準的な MQTT ブローカー（ポート `1883`）
* **TwinCAT 環境**: ADS over MQTT ルート設定済みの TwinCAT 3 ランタイム（ターゲットポート `851`）

### 使用 NuGet パッケージ

| パッケージ名 | バージョン | 用途 |
| :--- | :--- | :--- |
| `Beckhoff.TwinCAT.Ads` | `7.0.389` | ADS コア通信 (`AdsSession`, `IAdsConnection`) |
| `Beckhoff.TwinCAT.Ads.AdsOverMqtt` | `7.0.172` | ADS over MQTT トランスポート層 |
| `Beckhoff.TwinCAT.Ads.ConfigurationProviders` | `7.0.389` | インプロセスルーターの構成プロバイダ |
| `Beckhoff.TwinCAT.Ads.Reactive` | `7.0.389` | Reactive Extensions (`WhenNotification<T>`) |

---

## セットアップと設定変更

### 1. 接続パラメータの編集

現在、接続先は `AdsOverMqttTcRxService.cs`（および `AdsOverMqttService.cs`）内の `_configuration` にコード上で上書き設定されています（`appsettings.json` にも同様の構造が定義されています）。ご自身の環境に合わせて以下の値を変更してください。

```csharp
// クライアント側（このC#アプリ側）の仮想 AmsNetId
_configuration["AmsRouter:NetId"] = "10.10.10.10.1.1";
_configuration["AmsRouter:ChannelProtocol"] = "Mqtt";

// MQTT ブローカーおよび接続先 PLC の設定（配列インデックス :0: を指定）
_configuration["AmsRouter:Mqtt:0:Address"] = "192.168.3.54"; // MQTTブローカーのIPアドレス
_configuration["AmsRouter:Mqtt:0:Port"] = "1883";            // MQTTブローカーのポート番号
_configuration["AmsRouter:Mqtt:0:Topic"] = "AdsOverMqtt";    // TwinCAT側ルート設定と一致するトピック名
_configuration["AmsRouter:Mqtt:0:NetId"] = "15.15.15.15.1.1";// 接続先PLCのAmsNetId

// ターゲットPLCの AmsNetId と ADS ポート番号
AmsAddress targetPlcAddress = new AmsAddress("15.15.15.15.1.1", 851);
```

> **補足**: `appsettings.json` に定義されている `AmsRouter` セクションをそのまま使用する場合は、サービスクラス内の `_configuration["AmsRouter:..."]` の代入処理を削除することで、`appsettings.json` や環境変数からの設定読み込みに一本化できます。

### 2. TwinCAT 側のルート設定

TwinCAT 側のルート設定（`StaticRoutes.xml` または TwinCAT XAE の Route 設定）にて、上記で指定した **MQTT ブローカーのアドレス (`192.168.3.54`)**、**トピック名 (`AdsOverMqtt`)**、および **クライアント側の仮想 AmsNetId (`10.10.10.10.1.1`)** が通信できるよう構成されている必要があります。

### 3. 実行サービスの切り替え（任意）

デフォルトでは `Program.cs` で **Reactive Extensions 版 (`AdsOverMqttTcRxService`)** が登録されています。定期ポーリング版 (`AdsOverMqttService`) を試す場合は、`Program.cs` の該当行を切り替えてください。

```csharp
// デフォルト: Reactive Extensions による OnChange 通知監視
builder.Services.AddHostedService<AdsOverMqttTcRxService>();

// ポーリング版を使用する場合:
// builder.Services.AddHostedService<AdsOverMqttService>();
```

---

## ビルドと実行

プロジェクトディレクトリで以下のコマンドを実行します。

```bash
# 依存関係の復元とビルド
dotnet build

# アプリケーションの実行（デフォルトの http プロファイルを使用）
dotnet run
```

`Properties/launchSettings.json` の設定により、開発環境（`dotnet run`）ではデフォルトで **`http://localhost:5227`** で起動します（`https` プロファイル指定時は `https://localhost:7049` も有効になります）。

起動後、以下のコマンドまたはブラウザで稼働状況を確認できます。

```bash
curl http://localhost:5227/
# 出力: AdsOverMqtt Web Service is running.
```

---

## データ構造と監視対象シンボル

本サンプルでは、PLC 側のシンボル `demo3.runner.event_message` を監視し、以下の構造体（`TwinCATStructure.cs`）へマッピングします。

* **`StJobEventMessage`**:
  * `Subject`, `JobNamespace`, `JobId` (`STRING(255)` → `SizeConst = 256`)
  * `OldState`, `NewState` (`STRING(16)` → `SizeConst = 17`)
  * `NumOfJob` (`UDINT` → `uint`)
  * `RecordTime` (`ULINT` → `ulong`, `RecordDateTime` プロパティでローカル `DateTime` に自動変換)
* **`NcToPlcAxisRef`** (`TwinCAT.Ads.NcMapping` 名前空間):
  * NC 軸情報 (`MC.NCTOPLC_AXIS_REF` / 256 bytes) をマッピングするための拡張用構造体定義です。

> **Note**: `AdsOverMqttTcRxService` では、Rx の `.Where(...)` オペレータにより `NewState` または `OldState` が `"process"` のイベントのみをフィルタリングしてログ出力しています。すべての状態遷移を確認したい場合はフィルタ条件を調整してください。
