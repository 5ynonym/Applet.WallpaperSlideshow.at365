# Applet.WallpaperSlideshow.at365 開発ガイド

実装時のテスト選択、コミット前の必要回帰、リリース前のコミット/プッシュ確認と検証証跡の再利用は、[本体・Applet共通手順](../AppDock.at365/docs/development-workflow.md)に従います。この文書の試験コマンドは、その段階に応じて実行します。

利用方法は[README.md](README.md)、実測結果と未確認事項は[VERIFICATION.md](VERIFICATION.md)を参照してください。

## ビルド・発行・配置

Windows、.NET 10 SDK、隣の`AppDock.at365`ソースが必要です。

```powershell
dotnet build Applet.WallpaperSlideshow.at365.slnx -c Release
dotnet run --project Applet.WallpaperSlideshow.RegressionTests -c Release
.\publish.bat
.\deploy.bat "C:\Tools\AppDock"
```

発行EXEはwin-x64・自己完結型です。AppDock EXE隣の`extensions/Applet.WallpaperSlideshow.at365`へ配置します。引数なしの`deploy.bat`はGit除外の`deploy.local.txt`先頭行、なければ兄弟AppDockの`publish`へ配置します。スクリプトは実行中アプリを強制終了しません。AppDockを起動し直すと認識します。

`scripts/test-protocol.cjs` / `scripts/test-ui.cjs`はテスト専用エンジンと一時画像を使用し、ユーザーの壁紙・画像フォルダーを変更しません。

## Windows背景の準備（0.4.0）

AppDock0.21.0のsettingActionsからprepare-background/background-settingsコマンドを呼びます。再生・停止設定は変更せず、STAのdispatcherと既存commandGateで直列化します。実設定ボタンは自動実行しません。

IDesktopWallpaper.SetWallpaper/SetPosition(DWPOS_SPAN)と、HKCUのExplorer/WallpapersにあるBackgroundType=0（画像）の設定を組み合わせます。BackgroundTypeはWindows設定画面の実装に依存する値で、公開APIとして保証されていません。適用後は種類の読み戻し、GetStatusのDSS_SLIDESHOWがないこと、GetPositionのスパンを確認し、不一致/API失敗では手動手順付きエラーを返します。

現在表示中の壁紙はWindowsのThemes/TranscodedWallpaperキャッシュからBMPへコピーします。元のwallpaper.bmpは適用後に黒く上書きするので再適用しません。停止中は1px黒画像を設定し、停止状態を保ちます。windows-background.bmpはWindowsが参照するため維持します。キャッシュ欠落/破損では背景APIを呼ばず、手動設定を案内します。

`dotnet run --project Applet.WallpaperSlideshow.RegressionTests -c Release -- --inspect-background` はCOMの読み取りだけを確認します。回帰は画像/停止状態/結果不一致/API失敗をfakeで検証します。`scripts/test-background-settings-ui.cjs`は発行版AppDock＋native fixtureで、実壁紙を変更せずフォームと実コマンド経路を確認します。Windowsスライドショー/Spotlightからの実切替はこれらの試験とは別です。

参考: [SetPosition](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-idesktopwallpaper-setposition)、[GetStatus](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-idesktopwallpaper-getstatus)、[Windows設定URI](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings)。

## 画像処理・キャッシュの実装

0.4.1ではConst.AppDataFolderの基準をLocalApplicationDataへ変更した。Windows自身が管理するAPPDATA/Microsoft/Windows/Themes/TranscodedWallpaperは引き続き読み取り元にする。旧Roaming領域の移行・削除は追加しない。ホストが供給する履歴画像のimageDirectoryもPC専用rootを使用する。

画像の走査・合成は直列に実行し、更新を重ねず、キャンセル済みの結果を適用しません。設定が同じなら描画をやり直さず、監視の保守は5秒間隔です。抽選のシャッフルは線形時間。タイル画像は1枚ずつ読み込み・縮小・解放し、全タイルの原寸画像を同時に保持しません。

履歴画像は選択したモニターの表示ページだけ順次生成します。指定サイズのPNGをローカルファイルとして読み込み、通信量による画質・解像度の縮小は行いません。ページ変更・操作画面へ戻る・終了時にサムネイルを削除します。ブラウザー側でも画面外の画像を遅延読み込みします。専用トレイアイコン・トレイ項目・履歴ウィンドウは作りません。

一時BMPは`%LOCALAPPDATA%\at365\Applets\WallpaperSlideshow`、履歴サムネイルはAppDockのPC専用保存先の`cache/panel-images`内に保存します。強制終了時のキャッシュが残ることはあります。元アプリのデータとは分離しています。描画の実測と検証範囲は[VERIFICATION.md](VERIFICATION.md)を参照してください。

0.4.2以降、内部のAppLogは既存の[ホストLogging API](../AppDock.at365/docs/extensions.md#sdkで使えるサービス)の`IExtensionContext.Log.ErrorAsync`へ接続します。開始前に接続し、エンジンの停止後に切り離します。操作名と例外詳細をエラーとして送り、同じ操作/メッセージは1分間抑制します。最大128件の待ち行列を1つのバックグラウンド処理で送信し、1件の通信と終了時の排出待ちはそれぞれ最大1秒。キュー満杯や通信失敗時はstderrへ出し、壁紙処理を失敗させません。独自`errors.log`の作成・追記・ローテーションを廃止し、既存ファイルは変更しません。設定反映エラーもこの経路へ統一します。起動失敗は最大1秒だけ送信完了を待ち、ホストが起動失敗のプロセスを停止する前に記録します。

画像の削除はWindowsの[IFileOperationのごみ箱指定](https://learn.microsoft.com/ja-jp/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-setoperationflags)を使用します。

## Windowsのシャットダウン・ログオフ（0.4.3）

AppDockの通常終了はbefore-quitからdeactivateを呼ぶが、Electronの[before-quit](https://www.electronjs.org/docs/latest/api/app#event-before-quit)はWindowsのシャットダウン/再起動/ログオフでは発火しない。Appletの非表示DispatcherFormで[WM_ENDSESSION](https://learn.microsoft.com/en-us/windows/win32/shutdown/wm-endsession)を直接処理し、wParam=trueのとき、その通知から戻る前にengine.Stopを同期実行する。WM_QUERYENDSESSIONは許可だけを返し、wParam=falseの取消通知では何もしない。FormClosingのWindowsShutDownを確定通知と混同しない。

通常deactivate/通常FormClosing/セッション終了は同じStopEngineを使い、開始済みのエンジンだけを1回停止する。ApplicationController.PrepareShutdownはリソース解放で例外が出てもfinallyでClearWallpaperを行う。描画workerを無効化し、生成BMPを黒で上書きして既存のWindows壁紙解除APIを呼ぶ。AppDockへの非同期通信やUIのBeginInvokeを待ってから消去するものではない。壁紙のクリーンアップにはホストやSDKの変更は不要。終了時のエラーレベルを維持するログ受付のため、最低ホスト版は0.26.18とする。

[SetProcessShutdownParameters](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-setprocessshutdownparameters)でAppletプロセスを0x2ffに設定する。Windowsは大きい値から終了させるため、デフォルト0x280の親より先に処理する。設定失敗は診断ログへ記録する。セッション通知を拒否したり、終了確認ダイアログを追加したりしない。

`scripts/test-session-shutdown.cjs`は別プロセスの実DispatcherForm HWNDだけへ終了メッセージを送り、取消後の再生・同期黒BMP生成・二重停止防止・ログ排出を確認する。デスクトップAPIはfixtureで代替する。`scripts/test-host-logging-ui.cjs`は固定した発行AppDockとAppletを隔離起動し、全体/個別ログ画面とホストファイルを確認する。PC自体のシャットダウン、強制終了、電源断は自動試験しない。

## 文書の更新

READMEには動作環境・導入・操作・設定・利用上の制約を記載します。開発環境・ビルド・テスト・発行・開発者用配置・実装の説明はこのファイル、実測結果と未検証事項はVERIFICATION.mdへ記載します。共通方針は[AppDockのドキュメント方針](../AppDock.at365/docs/documentation.md)を参照してください。

## 更新配布物の発行

`publish.bat`は通常の発行先を生成した後、兄弟のAppDockリポジトリにある`scripts/pack-applet-update.ps1`で`publish/update.json`と`publish/update.zip`を自動生成します。共通パッカーのビルドに.NET 10 SDKが必要です。Gmail以外のAppletは、このパッケージ生成のためにNode.jsを導入する必要はありません。

ZIP直下に`extension.json`と実行ファイル一式を置き、JSONにID・版・必要な本体版・ZIPのサイズとSHA256を記録します。`OutputDirectory`を指定できる発行スクリプトでも、指定先の配布内容を読み、更新用JSON/ZIPの出力先はこのリポジトリの`publish`です。通常配置用サブフォルダーへJSON/ZIPを混ぜず、`deploy.bat`の配置対象も増やしません。

Web配布やGitHub Releaseには同じ発行で生成したJSONとZIPを一緒に置き、JSONを最後に公開してください。ソースコードの自動生成ZIPは使用しません。発行スクリプトから外部公開は行いません。[共通更新仕様](../AppDock.at365/docs/updates.md)と[配布先の確認手順](../AppDock.at365/docs/update-checklist.md)を参照してください。

## 開発生成物の保存先

開発・テストの生成物は`.artifacts`へ保存します。2026-10-10に旧`artifacts`を中身を保持して改名しました。過去の検証記録内の当repoの`artifacts/`は`.artifacts/`へ読み替えてください。保存済みログ/JSONの内部パスは実行当時の値として保持しています。作業完了時の整理は[AppDockの共通手順](../AppDock.at365/DEVELOPMENT.md#作業完了時のテストフォルダー整理)に従い、実行中・状態不明・未解決の失敗記録・再利用する資料を保持します。

## 外部コマンド宣言

AppDock 0.26.29以降のmanifest `commands[].automation`で公開対象を宣言します。本体側へApplet固有のIDを追加する必要はありません。公開契約・権限・動的登録の扱いは[共通操作API](../AppDock.at365/docs/automation.md#appletによる公開宣言)を参照してください。
