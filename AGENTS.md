# Applet.WallpaperSlideshow の作業指示

実装時のテスト選択、コミット前の必要回帰、リリース前のコミット/プッシュ確認と検証証跡の再利用は、[本体・Applet共通手順](../AppDock.at365/docs/development-workflow.md)に従います。この文書の試験コマンドは、その段階に応じて実行します。

作業開始時は[A:入口](../../AGENTS.md)、[共通開発指示](../AGENTS.md)、[ホスト指示](../AppDock.at365/AGENTS.md)を確認する。実装したモジュールは版を更新して自身のpublishへ発行する。commit・公開・実利用先へのdeployは別指示。

- 利用者向け仕様はREADME、開発手順はDEVELOPMENT、検証結果はVERIFICATIONへ分ける。
- 0.4.1以降の生成BMP（windows-background.bmpを含む）は`%LOCALAPPDATA%/at365/Applets/WallpaperSlideshow`へ保存する。旧Roaming領域の自動移行・削除は行わない。Windowsが参照する生成BMPは終了時に無条件削除しない。
- 0.4.2以降のエラーは既存のホスト`IExtensionContext.Log`へ送り、AppDockの全体ログとApplet詳細のログに表示する。独自errors.logは作成・追記しない。既存ファイルは保持する。同期のエンジンからは上限付きキューで直列送信し、重複抑制・通信失敗時のstderr・停止処理後の短い排出待ちを維持する。共通APIは[Applet API](../AppDock.at365/docs/extensions.md)を参照する。
- Windows自身が管理する`%APPDATA%/Microsoft/Windows/Themes/TranscodedWallpaper`は読み取り元として維持する。アプリの生成物の保存先と混同しない。
- Windowsのセッション終了は非表示DispatcherFormのWM_ENDSESSION（wParam=true）内で同期的にengine.Stopへ渡す。WM_QUERYENDSESSIONは許可だけを返し、取消で壁紙/再生を変えない。通常deactivateと同じStopEngineで二重実行を防ぐ。Appletの終了優先度は0x2ffとし、デフォルト0x280の親ホストより先にクリーンアップする。実OS終了と、限定HWNDへのメッセージ/壁紙API fakeによる試験の範囲を区別する。
- 設定はホストSettingsへ保存する。今後追加する共有素材やホスト管理の端末固有状態は[ホストの保存境界](../AppDock.at365/docs/settings-sync.md)に従う。履歴サムネイルはホストが渡すPC専用imageDirectoryを使う。
- 回帰とRPC fixtureではユーザーの壁紙・設定・画像を変更しない。Windows背景APIはfakeで検証し、公開EXEのRPC smokeは不正設定を拒否させて正常終了を確認する。
- GUI試験は直列で行い、実行中にホストのoutやpublishを再ビルドしない。作業完了時のテストフォルダー整理は[ホスト開発ガイド](../AppDock.at365/DEVELOPMENT.md#作業完了時のテストフォルダー整理)に従う。

- 外部公開コマンドは自身のextension.jsonで宣言する。ホストへ壁紙専用の許可一覧を戻さない。契約は[共通操作API](../AppDock.at365/docs/automation.md#appletによる公開宣言)を参照する。
