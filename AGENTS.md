# Applet.WallpaperSlideshow の作業指示

作業開始時は[A:入口](../../AGENTS.md)、[共通開発指示](../AGENTS.md)、[ホスト指示](../AppDock.at365/AGENTS.md)を確認する。実装したモジュールは版を更新して自身のpublishへ発行する。commit・公開・実利用先へのdeployは別指示。

- 利用者向け仕様はREADME、開発手順はDEVELOPMENT、検証結果はVERIFICATIONへ分ける。
- 0.4.1以降の生成BMP（windows-background.bmpを含む）とerrors.logは`%LOCALAPPDATA%/at365/Applets/WallpaperSlideshow`へ保存する。旧Roaming領域の自動移行・削除は行わない。Windowsが参照する生成BMPは終了時に無条件削除しない。
- Windows自身が管理する`%APPDATA%/Microsoft/Windows/Themes/TranscodedWallpaper`は読み取り元として維持する。アプリの生成物の保存先と混同しない。
- 設定はホストSettingsへ保存する。今後追加する共有素材やホスト管理の端末固有状態は[ホストの保存境界](../AppDock.at365/docs/settings-sync.md)に従う。履歴サムネイルはホストが渡すPC専用imageDirectoryを使う。
- 回帰とRPC fixtureではユーザーの壁紙・設定・画像を変更しない。Windows背景APIはfakeで検証し、公開EXEのRPC smokeは不正設定を拒否させて正常終了を確認する。
- GUI試験は直列で行い、実行中にホストのoutやpublishを再ビルドしない。作業完了時のテストフォルダー整理は[ホスト開発ガイド](../AppDock.at365/DEVELOPMENT.md#作業完了時のテストフォルダー整理)に従う。
