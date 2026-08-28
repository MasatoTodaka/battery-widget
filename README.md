# Logi Battery Widget

Windows常駐のデスクトップウィジェット。iOS/macOSのバッテリーウィジェット風の見た目で、
接続中の周辺機器(マウス・キーボード・ヘッドセットなど)のバッテリー残量を表示する。

## 構成

- `src/LogiBatteryWidget.Core` — バッテリー取得ロジック(UI非依存)
- `src/LogiBatteryWidget.App` — WPFアプリ(常時最前面フローティングウィジェット + トレイアイコン)

取得元は `IBatteryProvider` の実装として追加していく方式:

- `GHubBatteryProvider` — Logicool G HUB (`lghub_agent.exe`) がローカルに立てる
  `ws://localhost:9010` の非公式WebSocket APIを叩く。G HUBが起動していないと単に0件を返す
  (エラー扱いにしない)。公式ドキュメントはないが、実機のG HUB(PRO X SUPERLIGHT 2使用)で
  接続・デバイス一覧取得・バッテリー取得まで動作確認済み。ハンドシェイクに
  `Origin: file://` ヘッダーと `json` サブプロトコルが必須(付けないとHTTP 400で拒否される)。
- `WindowsBatteryProvider` — Windowsが標準で把握しているバッテリー付きデバイス
  (`Windows.Devices.Power.Battery`)を列挙する。ベンダー非依存だが、**Bluetoothの標準
  Battery Serviceで接続されているデバイスのみ**が対象。Logicool LightspeedやPulsar/Vaxee/
  ZOWIEなど独自2.4GHzドングル経由の接続はWindows自体がバッテリー情報を持たないため拾えない。

### 既知の制約: Logicool以外のベンダー

Pulsar / Vaxee / INZONE(Sony) / ZOWIE(BenQ) は、それぞれの純正ドングルで接続している場合、
G HUBのような読み取り可能なローカルAPIが公開されておらず(調査時点で確認できず)、
`WindowsBatteryProvider` にも掛からない。これらのデバイスをBluetoothで接続していれば
`WindowsBatteryProvider` 経由で拾える可能性がある。ドングル接続のバッテリーを表示したい場合は、
各社ソフトウェアの追加リバースエンジニアリングが必要になる(`IBatteryProvider` を実装して
`App.xaml.cs` の providers リストに追加すれば統合できる設計)。

## 実行方法

```
dotnet run --project src/LogiBatteryWidget.App/LogiBatteryWidget.App.csproj
```

- 起動するとデスクトップ右上にカードが表示される。ドラッグで移動でき、位置は次回起動時にも復元される。
- タスクトレイアイコンから「今すぐ更新」「表示/非表示」「終了」を操作できる。
- 更新間隔は既定60秒間隔のポーリング(`App.xaml.cs` で `BatteryMonitorService` に渡す
  `TimeSpan` を変更可能)。
