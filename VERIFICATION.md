# 検証記録

## 2026-10-08: 実利用先へのdeploy

- 配置後の実利用について、ユーザーが正常動作を確認したと報告（2026-10-08）。

- ユーザーの明示指示により、AppDockと全6Appletの`deploy.bat`を引数なしで実行し、7件すべて終了コード0。配置先は`A:\00.ESSENTIAL\00.MainTools\AppDock.at365`。5つの.NET Appletは現ソース/SDKで`publish.bat`を先に実行し、Gmailはdeploy内で再発行した。
- AppDock0.16.2、Gmail0.5.1、WallpaperSlideshow0.3.0、Watch0.1.1（native）、WebBrowserTools0.2.4、WindowMover0.2.1、WindowsTools0.1.1を配置。Watchの古いDLL版manifestを配置せず、現ソースのnative版へ更新。
- 配置対象21ファイルのSHA256はすべて発行元と一致。現ソースと配置manifestの版/runtime/entry、minimumHostVersionも照合。settings.json・avatar.png・Gmail accounts.jsonの3ファイルは配置前後のハッシュ不変。
- 配置前後とも関連プロセスなし。実利用アプリは起動していないため、次回起動で反映する。旧ファイル退避は行わず、設定・認証領域を配置スクリプトで変更していない。結果は`../AppDock.at365/artifacts/deploy-2026-10-08-result.json`（本体では`artifacts/deploy-2026-10-08-result.json`）。

## 2026-10-08: 依存パッケージ確認

- 外部NuGet PackageReferenceなし。slnxの`dotnet list package --outdated`も更新なし。依存定義や製品コード・版の変更は不要。参照するAppDockのnpm更新詳細は[本体検証記録](../AppDock.at365/VERIFICATION.md)を参照。
- 現AppDock SDK/RuntimeでRelease build警告0/エラー0、既存RegressionTests成功。実アプリ/ハードウェアに作用するnative検証、publish/deployは今回実施していない。

2026-10-06 JST。Applet.WallpaperSlideshow.at365 v0.1.0 / AppDock v0.5.0。

## 成功した確認

- .NET Releaseビルド、0警告・0エラー。Windows SDKパスのsandbox拒否（MSB4184）は、アクセス可能な実行環境で再確認した。
- 複数フォルダーの混合、親子・重複・欠落フォルダー、旧Folder設定、1巡まで重複なし、無効設定の拒否。
- タイル・負のモニター座標・クリッピング・画像の独立した寿命・キャンセル・直列化・古い結果の破棄・UI応答・一時BMPの終了時削除。
- フォルダー監視の再接続、削除・再作成、エラー復旧、部分走査、ログ世代交代と重複抑制、終了時のリソース解放。
- 実際のAppDock.Runtimeとテスト専用エンジンでactivate / command / settings.changed / deactivate / EOFを往復。トレイ項目0、13コマンド、停止状態の保存、不正設定時の前回設定維持、履歴4件/3件ページ、JPEGサムネイル、操作へ戻る際の画像解放。
- 製品EXEのWinFormsエントリーポイントを起動。不正設定をWindows壁紙APIに入る前に拒否し、deactivateとEOFで正常終了。
- パッケージ版AppDock（win-unpacked）で30秒既定値、JSON設定・保存、開始待ち、無効化、今すぐ開始、履歴・ページ送り、1280px/900pxの幅と画像表示、停止の保存を確認。JSページエラーなし。
- 必要ホスト99.0.0を持つテストAppletを同じAppDockで拒否し、開始待ち予約を作らないことを確認。
- AppDock本体・Applet両方の更新確認UIとリリースURLを、模擬GitHubレスポンスで確認。外部への問い合わせ・ブラウザー起動は行わない。
- AppDockの39件の自動テスト成功。既存Watch 8/8、WindowMover 23/23、WindowsTools 11/11成功。各Applet再publish成功。
- 壁紙Applet publish.bat成功。manifest・プロジェクト・発行EXEのバージョン一致。deploy.batで兄弟AppDockのpublish/extensionsへ配置し、EXE・manifestのSHA256一致。バッチはCP932の往復変換とCRLFを確認。

## 描画負荷の比較

テスト専用PerformanceProbeで、生成画像16枚（各2560×1440）を1920×1080の画面へTile合成。同じPC・各1回・別プロセス。ユーザー画像、Windows壁紙API、レジストリに触れない合成のみ。

| 指標 | 元実装 | 新Applet |
| --- | ---: | ---: |
| Private Memoryの描画前 | 10.9 MiB | 10.9 MiB |
| 合成中のピーク | 247.5 MiB | 34.9 MiB |
| 合成による追加ピーク | 236.6 MiB | 24.0 MiB |
| 合成時間 | 173 ms | 153 ms |
| CPU時間 | 187 ms | 156 ms |

追加ピークは約90%減。これはタイル合成の限定測定で、実際の常駐メモリや全画面構成に対する上限・CPU削減率を保証しない。デコーダー・画像サイズ・画面解像度・ファイル環境で変わる。

## 成果物

`publish/Applet.WallpaperSlideshow.at365/Applet.WallpaperSlideshow.at365.exe`

- ProductVersion: 0.1.0
- 51,697,863 bytes
- SHA256: D7AEFDCDCAE7BE9D2DC7E2A6EB2C2FE17B54098D7B03C078432F210740E487AC
- win-x64・自己完結型・単一EXE。extension.jsonを同梱。

## 検証境界

実際のWindows壁紙への適用、ロック/解除、RDP/ローカル切替、ネットワーク共有の切断復帰は今回実機で再現していない。元アプリのWindows APIと停止理由の処理を引き継ぎ、BMP生成と状態遷移・監視復旧は隔離テストで確認した。GitHubの実通信は未確認で、正式リリースが公開されていない場合は更新確認できない。
## 2026-10-06: v0.2.0 再生コマンド

- `start`（開始）と`stop`（停止）を追加。`toggle`（開始／停止の切り替え）と`next`（次の壁紙に更新）の表示名を整理し、操作パネルに4ボタンを表示。
- 従来の`pause` / `resume`コマンドIDは維持。履歴・データフォルダー・履歴ページ操作を含め15コマンド。
- 手動停止状態を保存。設定の再読込に依存せず再生状態を変更するため、保存済み画像設定に誤りがあっても前回の有効設定で開始・停止できる。ロック/RDPによる停止理由は維持。
- Release回帰テスト、公開EXEの安全な起動確認、実プロトコル往復成功。4コマンド、停止中の次画像抑制、旧ID、不正設定後の開始・停止を確認。
- パッケージ版AppDockのGUIで開始・停止・次の壁紙・開始停止切り替えと保存を確認。テスト専用エンジンなので、実際のWindows壁紙は変更していない。
- publish / deploy成功。兄弟AppDockのpublish/extensionsのEXE・manifestは発行元とSHA256一致。
- 最新EXE: ProductVersion 0.2.0、51,698,163 bytes。
- SHA256: 819D2BB4E590121FB27954DA91C890F15559945F569665F4F70A1EF77B997C16。
## 2026-10-06: v0.3.0 / AppDock v0.6.0

- モニターごとのフォームを実装。複数フォルダー、フォルダー選択、表示モード、タイル枚数、上下左右のPadding、追加・並べ替え・削除を確認。旧JSON文字列・Folder・数値enumと未知フィールドを保持。
- 元アプリの履歴設定範囲を復元。1000pxのサムネイルを実表示し、ファイル名1～1024文字と相対Folderを受け付ける。更新間隔・タイル間余白・モニター別Padding・履歴保存件数はすべて設定可能。
- 開始／再開をstartへ統合。resume / pauseはホストの別名として旧ショートカットを保持。停止・切り替え・次画像、履歴・ページ送り・操作画面・データフォルダーの9コマンド。画像ごとのopen/deleteやモニター選択は一般コマンドへ登録せずパネル専用操作で実行。
- manifest宣言でロード前からコマンド・ショートカット設定が可能。明示startは無効・開始待ちから有効化して即時起動し、互換性チェック・起動キュー・タイマー取消を共有。
- 履歴をモニター別タブに分割し、件数・ページ位置を別々に管理。1ページ表示件数は既定4件、1～1000件。GUI保存とRPCで2 / 4 / 16件を確認。
- ローカルPNG画像を専用キャッシュから表示。通信量による画質や解像度の引き下げなし。表示ページの画像のみ逐次生成し、ページ切替・操作画面・deactivateでファイルを解放。大きな一時画像が200000 bytesを超えても表示でき、1000pxの設定サイズを保つことを確認。
- 拡大プレビュー、元ファイルのツールチップ、「画像を開く」の隣の「画像を削除」を実装。削除対象の確認とキャンセル、成功後の全モニター履歴からの除去・更新を確認。古いページ世代のボタンと二重確認を拒否。
- Windows IFileOperationのごみ箱処理を一意な4-byte一時ファイルで実機確認。ごみ箱に存在すること、復元後の全バイト一致を確認し、一時ファイルを片付けた。GUI/RPCテストの削除はテスト専用画像だけを対象にした。
- .NET Release回帰、0警告・0エラー、公開EXEの不正設定拒否と正常終了、実RuntimeのRPC往復成功。
- AppDockの44件の回帰テスト成功。キャッシュ外の画像拒否、local-images能力検査、現パネルの操作だけ許可、宣言コマンドの旧ID・同時開始・必要ホスト拒否を含む。
- win-unpackedの実GUIで1280 / 900px、旧設定からのフォーム編集・保存、モニタータブ、表示件数変更、拡大、削除確認・キャンセル・再表示、開始再開・停止・切り替えを確認。JSページエラーなし。スクリーンショットも目視確認。
- AppDock単一EXE v0.6.0の隔離smoke成功（bridge・sandbox・アバター・設定）。本体とAppletの更新確認は模擬GitHub応答で確認し、外部公開・実通信なし。
- 最新Applet EXE: ProductVersion 0.3.0、51,703,190 bytes、SHA256 01A12DE0131FDE033C3DD1F3CAC90837D3D0F81F9365FB4E871C5DEAD6E34293。

実際のユーザー壁紙の切り替え、ロック/RDP、共有の切断復帰は今回のUIテストでは操作していない。前節の描画・監視・停止理由の回帰で確認し、実機検証の境界は維持する。

- 対応AppDock EXE: v0.6.0、100,413,527 bytes、SHA256 DB616106277E010FAC5E1D593504E0DA62C1027BABD193F3DC2C42F0082C1C9B。
- 実利用先 `A:\00.ESSENTIAL\00.MainTools\AppDock.at365` のAppDock v0.6.0と壁紙Applet v0.3.0へ配置。ユーザーが設定を保存してAppDockを完全終了してから実施した。ホストEXE・Applet EXE・manifestのSHA256は発行元と一致し、既存settings.jsonは前後のSHA256が同一。
