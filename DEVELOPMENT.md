# Applet.WallpaperSlideshow.at365 開発ガイド

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

## 画像処理・キャッシュの実装

画像の走査・合成は直列に実行し、更新を重ねず、キャンセル済みの結果を適用しません。設定が同じなら描画をやり直さず、監視の保守は5秒間隔です。抽選のシャッフルは線形時間。タイル画像は1枚ずつ読み込み・縮小・解放し、全タイルの原寸画像を同時に保持しません。

履歴画像は選択したモニターの表示ページだけ順次生成します。指定サイズのPNGをローカルファイルとして読み込み、通信量による画質・解像度の縮小は行いません。ページ変更・操作画面へ戻る・終了時にサムネイルを削除します。ブラウザー側でも画面外の画像を遅延読み込みします。専用トレイアイコン・トレイ項目・履歴ウィンドウは作りません。

一時BMPと`errors.log`は`%AppData%\at365\Applets\WallpaperSlideshow`、履歴サムネイルはAppDockの`.appdock/cache/panel-images`内に保存します。強制終了時のキャッシュが残ることはあります。元アプリのデータとは分離しています。描画の実測と検証範囲は[VERIFICATION.md](VERIFICATION.md)を参照してください。

画像の削除はWindowsの[IFileOperationのごみ箱指定](https://learn.microsoft.com/ja-jp/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-setoperationflags)を使用します。

## 文書の更新

READMEには動作環境・導入・操作・設定・利用上の制約を記載します。開発環境・ビルド・テスト・発行・開発者用配置・実装の説明はこのファイル、実測結果と未検証事項はVERIFICATION.mdへ記載します。共通方針は[AppDockのドキュメント方針](../AppDock.at365/docs/documentation.md)を参照してください。
