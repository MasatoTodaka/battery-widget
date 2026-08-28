# Logi Battery Widget

Windows常駐のデスクトップウィジェット。iOS/macOSのバッテリーウィジェット風の見た目で、
接続中の周辺機器(マウス・キーボード・ヘッドセットなど)のバッテリー残量を表示する。

## 構成

- `src/LogiBatteryWidget.Core` — バッテリー取得ロジック(UI非依存)
- `src/LogiBatteryWidget.App` — WPFアプリ(デスクトップ常駐フローティングウィジェット + タスクトレイ)

取得元は `IBatteryProvider` の実装として追加していく方式:

- `GHubBatteryProvider` — Logicool G HUB (`lghub_agent.exe`) がローカルに立てる
  `ws://localhost:9010` の非公式WebSocket APIを叩く。G HUBが起動していないと単に0件を返す
  (エラー扱いにしない)。公式ドキュメントはないが、実機のG HUB(PRO X SUPERLIGHT 2使用)で
  接続・デバイス一覧取得・バッテリー取得まで動作確認済み。ハンドシェイクに
  `Origin: file://` ヘッダーと `json` サブプロトコルが必須(付けないとHTTP 400で拒否される)。
- `WindowsBatteryProvider` — Windowsが標準で把握しているバッテリー付きデバイス
  (`Windows.Devices.Power.Battery`)を列挙する。ベンダー非依存だが、**Bluetoothの標準
  Battery Serviceで接続されているデバイスのみ**が対象。純正2.4GHzドングル経由の接続は
  Windows自体がバッテリー情報を持たないため拾えない。
- `InzoneBatteryProvider` — Sony INZONEのUSBドングル(INZONE Buds等)のHIDコントロールチャネル
  (vendor `0x054C`, usage page `0xFF04`, 64バイトレポート)に直接コマンドを送ってバッテリーを
  読む。INZONE Hubのインストールは不要(HIDデバイスは複数ハンドルから同時オープン可能なため、
  INZONE Hubが動いていても共存できる)。プロトコルはSonyの公式ドキュメントがなく、コミュニティの
  リバースエンジニアリング成果([penguinwokrs/openinzone](https://github.com/penguinwokrs/openinzone)
  の`docs/PROTOCOL.md`、GPL-3.0)に書かれた仕様を元に独自実装したもの(コードのコピーではない)。
  実機のINZONE Budsドングルで接続・左右イヤホン/ケースのバッテリー取得まで動作確認済み。パケット
  組み立て/解析は`LogiBatteryWidget.Core/Providers/Inzone/InzoneHciPacket.cs`、デバイス列挙は
  `InzoneHidLocator.cs` を参照。
- `VaxeeBatteryProvider` — VAXEE(ZYGENブランド含む)ワイヤレスマウスのドングルのHID
  **フィーチャーレポート**コマンドチャネル(vendor `0x3057`, usage page `0xFF05`, 64バイト)に
  直接コマンドを送ってバッテリーを読む。プロトコルはVAXEEの公式ドキュメントがなく、コミュニティの
  ドキュメント([stuffz/mouse-battery-monitor](https://github.com/stuffz/mouse-battery-monitor)
  の`docs/VAXEE.md`、ライセンス表記なし)に書かれた仕様を元に独自実装したもの(コードのコピー
  ではない)。実機のVAXEE 4Kドングルで動作確認済み。**既知の癖**: マウスのワイヤレスリンクが
  アイドル状態だと、ドングルはエラーではなく全ゼロの無応答を返す(コマンドIDのエコーがない)。
  この場合このプロバイダーは単に「今回は取得できなかった」として扱う——マウスを少し動かせば
  次のポーリングで復帰する。

### 既知の制約: 上記以外のベンダー

Pulsar / ZOWIE(BenQ) は、それぞれの純正ドングルで接続している場合、上記のような読み取り可能な
ローカルAPI/HIDプロトコルの情報が見つからず(調査時点で確認できず)、`WindowsBatteryProvider`
にも掛からない。これらのデバイスをBluetoothで接続していれば`WindowsBatteryProvider`経由で
拾える可能性がある。ドングル接続のバッテリーを表示したい場合は、各社ソフトウェアの追加
リバースエンジニアリングが必要になる(`IBatteryProvider`を実装して`App.xaml.cs`のproviders
リストに追加すれば統合できる設計)。

## 実行方法

```
dotnet run --project src/LogiBatteryWidget.App/LogiBatteryWidget.App.csproj
```

- 起動するとデスクトップ右上にカードが表示される。ドラッグで移動でき、位置は次回起動時にも
  復元される。常時最前面ではなく、他のアプリウィンドウより背面・デスクトップより前面に位置する
  (Chrome等を開くと自動的に隠れる)。
- 操作はすべてタスクトレイアイコンの右クリックメニューから:「今すぐ更新」「設定...」
  (表示するデバイスの選択・並べ替え、表示位置の四隅プリセット)「ウィジェットを表示/非表示」
  「終了」。
- 更新間隔は既定45秒間隔のポーリング(`App.xaml.cs` で `BatteryMonitorService` に渡す
  `TimeSpan` を変更可能)。
