# 検証記録

## 2026-10-10: 0.4.3 コミット前の全回帰確認

- 共通ログ/Windows終了対応のコミット依頼により、対象19ファイルをstageしてからRelease標準回帰8グループを再実行して成功。検証開始/終了のtreeは`7b385e05185f67aa748c7fdb2013758c40dde874`で一致し、未stage/未追跡の入力なし。
- `test-protocol.cjs`のRPC8項目と発行native EXEの起動失敗ログ・実HWND query/cancel/end、`test-session-shutdown.cjs`の確定/logoff/通常停止3群、`test-host-logging-ui.cjs`の固定本体/実AppletログGUI4群も再実行してすべて終了0。結果は`.artifacts/session-shutdown-1791638882618/result.json`と`.artifacts/host-logging-1791638884710/result.json`。取消後再生、通知から戻る前の黒BMP生成、1回のcleanup、停止時errorレベルの共通ログ排出を確認。
- 兄弟AppDockは型検査、標準回帰192/192、変更コード書式確認が成功。発行物0.26.18/0.4.3のhash/size、feed/ZIP全2ファイル、文書リンク95件、既存全体ZIP不変を照合。製品ソース/テスト/依存/発行物は検証前後で不変のため再発行は不要。
- 再利用用証跡は`.artifacts/commit-validation-20261010-logging-shutdown/`。各コマンド/終了コード/完全ログSHA256、tracked入力、兄弟SDK/Runtime、toolchain/lockfile/依存状態と発行物を保存。検証後の変更は本VERIFICATIONへの記録追加のみで、最終treeとコミットSHAを同証跡に記録する。
- 完了時整理: 古い成功のhost-logging/session-shutdown各1件を、専用プロセス終了・直下の絶対パス・reparse pointなしを確認して削除。方式別最新成功3件・失敗記録・再利用証跡を保持（cleanup.json）。PC自体のshutdown/reboot/logoff・実壁紙APIは未試験で、隔離実HWND/fake APIの境界を維持。今回の明示依頼は両repoのローカルコミットまで。

## 2026-10-10: 0.4.3 共通ログとWindows終了時クリーンアップ

- 0.4.2で独自errors.logを廃止し、エンジン/監視/画像処理/設定/履歴のエラーを既存のLog.ErrorAsyncへ接続。重複抑制（1分）と上限128件の直列送信、通信失敗のstderr、停止後の最大1秒排出待ちを確認。既存errors.logの内容を変更せず、新規ファイルも作らない回帰が成功。起動失敗は送信完了を最大1秒待ってから元のエラーを返す。
- 追加依頼のWindows終了対応を含む最終版は0.4.3。非表示DispatcherFormがWM_QUERYENDSESSIONを許可し、取消で再生を変えず、WM_ENDSESSIONの確定時に同期的に同じStopEngineを実行。終了優先度0x2ffと、リソース解放の例外時も壁紙消去を試みるfinallyを追加。通常停止との重複は1回。ログの停止中受付修正のため最低AppDockは0.26.18。
- 最終Release回帰8グループ成功（.artifacts/session-shutdown-regression.log）。画像/背景API/描画・監視の既存回帰、Logの送信・重複・再接続・切断・無応答・キュー上限、実HWNDへのquery/取消/確定/logoffと黒BMPを確認。デスクトップAPIはfake。
- test-protocol.cjsの8項目と発行native EXEの不正設定拒否・起動失敗ログ・実透明tool HWNDへのquery/cancel/end・正常終了が成功（.artifacts/session-shutdown-native-protocol.log）。WinFormsの補助HWNDも同じWindowクラスだったため、最初の列挙試験は失敗。実測した透明tool-windowの拡張styleで対象を限定して最終成功。製品コードには試験用コマンドを追加していない。
- test-session-shutdown.cjsは別プロセスの実DispatcherFormへ限定メッセージを送信。確定終了・logoff・通常deactivateの3群で、取消後の再生、通知から戻る前の黒BMP生成/空の壁紙パスへの適用、重複停止の防止、クリーンアップログの排出・正常RPC終了を確認（.artifacts/session-shutdown-1791637415929/result.json）。
- 最終固定AppDock 0.26.18単一EXEと0.4.3のGUIは4群成功（.artifacts/host-logging-1791637417933/result.json）。fixture内部の画像エラーと実発行Appletの起動エラーについて、全体/個別ログ画面・Applet ID/errorレベル・ホストログファイル・重複抑制・正常終了・独自errors.logなしを確認。画面画像を目視。停止処理中のfixtureエラーもerrorとして保存。旧本体では起動失敗の即時終了と停止中API拒否があり、前者をAppletで短い送信待ち、後者を本体0.26.18のログだけの受付で修正した。
- publish.bat終了0。EXE0.4.3は51,710,580bytes/SHA256 75ef53ac044be384c546fc3dfdd62151aaceb68970103c1c69574056e0bbfb89。update.zipは46,268,101bytes/SHA256 855a94bbe98219e450d91514a73338be3ea16be6fadb75c18edbe04afbf2c2fe。ソース/発行manifest・最低host・feed・ZIP全2ファイル・GUI検証コピーが一致（.artifacts/logging-shutdown-final-check.json）。関連文書リンク95件と差分検査成功。
- 完了時整理: host-loggingの成功3回とsession-shutdownの成功3回を保持。解決済み失敗profileと、旧ui群の結果/利用状況が不明な記録は保持し、.artifactsの追加削除0。旧GUI fixtureが残した自身のTemp画像3フォルダーは、ログのGUID/親パス/画像だけの内容/非転送/プロセス終了を確認して限定削除（logging-fixture-temp-cleanup.json）。今後のfixtureはdeactivate応答前に自身の一時画像を削除する。
- 実PCのシャットダウン/再起動/logoff、Windows壁紙APIの実状態、強制終了/電源断は未試験。Windowsの終了順全体や別アプリによる終了取消は実OS操作での確認が必要。ユーザーの壁紙・画像・設定と旧ログは変更していない。実装とローカルpublishまでで、commit/push/Release/deployなし。

## 2026-10-10: 開発生成物を`.artifacts`へ改名

- ユーザー指定でartifacts→.artifactsを改名。移動直後に既存1876項目の相対パス/size/mtime/ディレクトリ・リンク属性が一致し、検証終了時も元の全項目のsize/mtime/属性が不変。配布物4ファイルのSHA256も検証前後で一致。保存済みログ/JSONは内部パスを含めて保持し、過去記録の当repoのartifacts/は.artifacts/へ読み替える。
- テスト/開発用の参照とGit除外/開発手順を更新。6repo合計の変更CJS18件の構文、Gmail start-dev.ps1の構文/UTF-8 BOM、各repoのgit diff --checkが成功。旧artifactsの再生成なし、新.artifactsのGit除外を確認。
- 既存.NET回帰5群成功、scripts/test-protocol.cjsのfixture7群と発行済みnativeの不正設定拒否/終了確認成功。実利用の壁紙は変更していない。
- ログは.artifacts/rename-20261010-regression.log。Gmail/Wallpaper/Watchの元の統合試験ログは.artifacts/rename-20261010-integration.log。残りの開始/停止確認の再現スクリプト/ログはA:/XX.TEMP/applets-artifacts-rename-startstop-20261010.cjsと同.log。確認スクリプトのsnapshot非同期取得/待機の途中失敗は修正し、最終は4件すべて終了0。棚卸し/最終照合はA:/XX.TEMP/applets-artifacts-rename-20261010-{before,after,final}.json。
- 製品実装は変更せず、manifest版0.4.0と既存publishを保持。再発行/commit/push/Release/実利用deployなし。同期・バックアップ設定はユキちゃんが担当。今回の成功した新規profileは各方式で直近3回以下、古い証跡は使用終了/再利用要否を一括確定していないため保持し削除0。

## 2026-10-09: 更新配布物の自動生成

- `codex/update-packages`で発行スクリプトだけを更新。Applet本体の版は0.4.0を維持し、`publish.bat`終了コード0。共通パッカーはAppDock 0.23.0のソースから発行。
- `publish/update.json`のID・版をmanifestと照合し、ZIPのサイズ46263136bytesとSHA256 `695eebf2dfbc81f603b505eaf3ba93ab16f16fdc59680c201beb308e1488a998`を照合。ZIP内2ファイルすべてを通常発行フォルダーとバイト単位で比較し一致。収録: `Applet.WallpaperSlideshow.at365.exe`, `extension.json`。
- 旧SDK/旧DLLの生成物が残るWatch・WindowMover・WindowsToolsでは、既存deployと一致する配布内容へ整理する処理を追加。任意のユーザーファイルの再帰削除は行わない。
- 共通検証結果はAppDockの`artifacts/applet-update-packages.json`、発行ログは`artifacts/Applet.WallpaperSlideshow.at365-update-publish.log`。実利用先deploy・外部公開・pushは未実施。実GitHub/HTTP(S)/UNC配布先の確認はユーザーが後で行う。Applet固有機能・実アカウント操作の再試験は今回の発行変更の対象外。

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

## 2026-10-08: 0.4.0 Windowsの背景設定

- 必要AppDockを0.21.0へ更新。設定ページに「Windowsの背景を『画像・スパン』に設定」と「Windowsの背景設定を開く」を追加し、近くに手動手順を表示。
- Release build（警告0/エラー0）、全既存回帰＋BackgroundTests、test-protocol.cjs、publish.bat成功。BackgroundTestsはキャッシュの画像保持、停止中の黒背景、種類/スパン不一致、API失敗、キャッシュ欠落、COM解放/一時ファイル整理をfakeで確認。
- 通信fixtureは背景設定の成功/失敗と停止維持、背景設定ページを開くコマンドを確認。既存の開始/停止/次/設定エラー維持/モニター別履歴/削除/終了も成功。
- 最終AppDock0.21.0の配布GUI: `artifacts/background-settings-1791464780421/result.json`、ok:true。手動案内、実行中無効化、成功/失敗表示、未保存入力/停止状態保持、設定データに操作情報が入らないこと、停止時ボタン無効化、900×720 DIP横はみ出しなしを確認。success/small画像を目視確認。
- 実Windows COMの読み取り: --inspect-backgroundでpicture:true/span:true。実際の壁紙・Windows設定は変更していない。Windowsスライドショー/Spotlight/単色から画像への実切替、組織ポリシー/RDP制限、実設定ページを開く操作は未確認。BackgroundTypeとThemesキャッシュはWindows実装に依存し、API/状態確認が失敗すれば手動手順付きエラーを返す。
- 発行manifestはソースとSHA256一致。実利用先deploy、commit、pushなし。
- 初回sandboxの.NET buildは理由を伴わず失敗したが、許可された通常Windows環境でbuild/回帰/publish成功。
- 最終Applet EXE: 51705880 bytes、SHA256 4462DD32341C2F57F11EA2315009F146526838FFF4FDA1B1F6EBB0BED985BFD0。
## 2026-10-10: 0.4.1 生成画像をLOCALAPPDATAへ保存

- Const.AppDataFolderをLocalApplicationDataへ変更し、生成BMP（windows-background.bmpを含む）・errors.log・データフォルダー操作を%LOCALAPPDATA%/at365/Applets/WallpaperSlideshowへ統一。Windows自身のAPPDATA/Microsoft/Windows/Themes/TranscodedWallpaperは読み取り元として維持。旧Roamingファイルの移動・削除なし。設定はホストSettings、履歴サムネイルはホストPC専用imageDirectory。
- Release回帰6グループ成功。既定保存先も検査し、生成画像/停止時黒背景/キャッシュ欠落/API失敗/COMとファイル解放/描画/監視復旧をfakeで確認（.artifacts/localappdata-regression.log）。RPC fixture7項目と発行native EXEの不正設定拒否・正常終了が成功（localappdata-protocol.log）。実ユーザーの壁紙やWindows設定は変更していない。
- publish.bat終了0。EXE ProductVersion0.4.1、51707296bytes、SHA256 703ce4cfd8a120a35fe902d26511e63f198d534f634f44e363aff9d3bb02310e。update.zip46264607bytes、SHA256 ab2170b748a33d5741ebe89159e910326bd12773e2de0c6abc52371223d05f7b。最低host0.21.0維持。feedサイズ/hash/ZIP全2ファイルと発行元一致（localappdata-final-check.json）。
- protocol一時画像はランナー終了時に削除済み。完了時整理で成功の実行方式・利用状況が不明な旧記録/再利用資料を保持し追加削除0。新規AGENTSと利用/開発文書を整備。ユーザー指定により今回変更をコミットする。push/Release/実利用deployなし。
