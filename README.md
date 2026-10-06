# Applet.WallpaperSlideshow.at365

WallpaperSlideshow.at365をAppDockへ移したWindows用Appletです。**v0.3.0 / AppDock v0.6.0以降が必要**です。

## 設定

AppDockの「設定」→「Applet.WallpaperSlideshow.at365」で、JSONを直接書かずに設定できます。

- 「モニターを追加」で画面ごとの設定を作成。左から右、同じX位置では上から下の順です。並べ替え・削除も可能です。
- モニターごとに画像ソースフォルダーを複数追加でき、Windowsのフォルダー選択も使えます。各64件まで。旧設定の単一`Folder`やJSON文字列も読み取り、編集時に`Folders`へ移します。
- **表示モード・タイル枚数・上下左右のPaddingはモニターごとの設定**です。Fill / Fit / Stretch / Center / Tile、タイル1～64枚、Padding 0～65535px。実際の画面サイズを超える余白はエラーにします。
- 更新間隔、タイル間余白、履歴の保存件数、サムネイルの幅・高さ、ファイル名の表示文字数をフォームで変更できます。元アプリの範囲（サムネイル1～2048px、ファイル名1～1024文字）を維持しています。
- 「最近使った壁紙の1ページの表示件数」は**既定4件、1～1000件**です。モニターごとの履歴保存件数（既定30件、0～1000件）とは独立しています。

フォルダー内のJPEG / PNG / BMPとサブフォルダーの画像を使います。重複フォルダーや親子フォルダーの画像を重複排除し、1巡するまで同じ画像を選びません。存在しないフォルダーや共有の復旧を監視し、アクセスできるほかのフォルダーは使えます。サブフォルダーのジャンクション・シンボリックリンクはたどりません。絶対パスを推奨しますが、旧設定の相対パスもAppletの配置フォルダーを基準に解釈します。

設定はAppDockの`settings.json`内の`extensions["at365.wallpaper-slideshow"].settings`へ保存し、元アプリの設定は変更しません。不正な画像設定はログとパネルに表示し、前回の有効設定を維持します。旧`Mode`の数値表現も引き継ぎます。

## 遅延開始とコマンド

AppDock共通の「開始までの秒数」は**既定30秒**。有効化・AppDock起動・Applet再起動から指定秒数後に、Appletのプロセス自体を起動します。0で即時、最大86400秒。開始待ちの画面に予定時刻と「今すぐ開始」を表示します。無効化・AppDock終了で予約を取り消し、待機中の秒数変更ではそこから待ち直します。

コマンドはAppDockのパレット・ピン留め・ショートカットで使用します。manifestで宣言したコマンドはロード前から設定できます。「開始／再開」と「開始／停止切り替え」を明示的に実行すると、無効・開始待ちのAppletも有効化して即時起動します。

| ID末尾 | 操作 |
| --- | --- |
| `start` | 開始／再開。手動停止を解除し、再生中なら維持 |
| `stop` | 停止。現在の壁紙を消去し、停止状態を保存 |
| `toggle` | 手動停止状態を反転して保存 |
| `next` | 次の壁紙に更新 |
| `history` | 最近使った壁紙をAppDock内に表示 |
| `history.previous` / `history.next` | 選択中モニターの履歴をページ送り |
| `home` | 壁紙の操作画面に戻る |
| `data` | データフォルダーを開く |

IDの接頭辞は`at365.wallpaper-slideshow.`です。旧`resume`は`start`、旧`pause`は`stop`の別名として既存ショートカットを維持します。未割り当ての別名を新規コマンド一覧に重複表示しません。画像ごとの「履歴1を開く」等はコマンド登録せず、履歴ビュー専用のボタンで処理します。

手動停止は保存され、ロック解除で勝手に再開しません。ロック・リモート接続による停止は手動停止と独立しています。停止中の「次の壁紙」は切り替えず、開始もロック・リモート接続による停止を解除しません。不正設定を保存しても開始・停止は前回の有効設定で操作できます。停止・無効化・終了時の壁紙消去は元アプリの動作を引き継ぎます。元アプリとの同時再生は避けてください。

## モニターごとの履歴

履歴の上部にある**モニター1 / モニター2…**で切り替えます。各モニターの件数とページ位置を別々に扱います。履歴は実行中だけパスを保持し、Applet終了で消えます。0件設定で履歴を無効化できます。

各画像にサムネイル、解像度・ファイルサイズ、元ファイルパスのツールチップ、「画像を開く」「画像を削除」を表示します。サムネイルをクリックすると拡大表示できます。削除は対象の元ファイルを確認する画面を表示し、「ごみ箱へ移す」で実行します。キャンセルも可能です。成功時は同じパスをすべてのモニターの履歴から除き、表示を更新します。古いページのボタンでは別の画像を操作できません。

Windowsの[IFileOperationのごみ箱指定](https://learn.microsoft.com/ja-jp/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-setoperationflags)を使用します。削除失敗時はファイルと履歴を維持します。ごみ箱へ移せない環境ではエラーになります。

## 負荷とデータ

画像の走査・合成は直列に実行し、更新を重ねず、キャンセル済みの結果を適用しません。設定が同じなら描画をやり直さず、監視の保守は5秒間隔です。抽選のシャッフルは線形時間。タイル画像は1枚ずつ読み込み・縮小・解放し、全タイルの原寸画像を同時に保持しません。

履歴画像は選択したモニターの表示ページだけ順次生成します。指定サイズのPNGをローカルファイルとして読み込み、通信量による画質・解像度の縮小は行いません。ページ変更・操作画面へ戻る・終了時にサムネイルを削除します。ブラウザー側でも画面外の画像を遅延読み込みします。専用トレイアイコン・トレイ項目・履歴ウィンドウは作りません。

一時BMPと`errors.log`は`%AppData%\at365\Applets\WallpaperSlideshow`、履歴サムネイルはAppDockの`.appdock/cache/panel-images`内に保存します。強制終了時のキャッシュが残ることはあります。元アプリのデータとは分離しています。描画の実測と検証範囲は[VERIFICATION.md](VERIFICATION.md)を参照してください。

## バージョン確認とビルド

保存されるモニター設定の形式例です。通常の設定操作には上記のフォームを使ってください。

```json
[
  { "Folders": ["C:/Wallpapers/Art", "C:/Wallpapers/Photos"], "Mode": "Tile", "TileCount": 13, "PaddingLeft": 1, "PaddingRight": 1, "PaddingTop": 1, "PaddingBottom": 40 },
  { "Folders": ["C:/Wallpapers/Portrait"], "Mode": "Fit", "PaddingLeft": 1, "PaddingRight": 1, "PaddingTop": 1, "PaddingBottom": 1 }
]
```

manifestの`minimumHostVersion: "0.6.0"`に満たないAppDockではロードしません。AppDock本体と各Appletの「更新を確認」は手動でGitHubの正式リリースを調べます。Appletの問い合わせ先は`5ynonym/Applet.WallpaperSlideshow.at365`です。自動ダウンロード・更新は行いません。

Windows、.NET 10 SDK、隣の`AppDock.at365`ソースが必要です。

```powershell
dotnet build Applet.WallpaperSlideshow.at365.slnx -c Release
dotnet run --project Applet.WallpaperSlideshow.RegressionTests -c Release
.\publish.bat
.\deploy.bat "C:\Tools\AppDock"
```

発行EXEはwin-x64・自己完結型です。AppDock EXE隣の`extensions/Applet.WallpaperSlideshow.at365`へ配置します。引数なしの`deploy.bat`はGit除外の`deploy.local.txt`先頭行、なければ兄弟AppDockの`publish`へ配置します。スクリプトは実行中アプリを強制終了しません。AppDockを起動し直すと認識します。

`scripts/test-protocol.cjs` / `scripts/test-ui.cjs`はテスト専用エンジンと一時画像を使用し、ユーザーの壁紙・画像フォルダーを変更しません。
