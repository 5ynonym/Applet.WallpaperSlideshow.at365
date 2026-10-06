# 検証記録

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