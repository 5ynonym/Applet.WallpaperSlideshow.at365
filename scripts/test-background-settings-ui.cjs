const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { createRequire } = require('node:module');
const root = path.resolve(__dirname, '..');
const host = path.resolve(root, '../AppDock.at365');
const requireHost = createRequire(path.join(host, 'package.json'));
const { _electron: electron } = requireHost('playwright');
const profile = path.join(root, '.artifacts', `background-settings-${Date.now()}`);
const folder = path.join(profile, 'extensions', 'wallpaper');
fs.mkdirSync(folder, { recursive: true });
fs.cpSync(path.join(root, 'Applet.WallpaperSlideshow.RegressionTests/bin/Release/net10.0-windows'), folder, { recursive: true });
const manifest = JSON.parse(fs.readFileSync(path.join(root, 'extension.json'), 'utf8'));
manifest.entry = 'Applet.WallpaperSlideshow.RegressionTests.exe';
manifest.startupDelaySeconds = 0;
fs.writeFileSync(path.join(folder, 'extension.json'), JSON.stringify(manifest, null, 2));
const { createDefaultSettings } = requireHost('./out/main/shared/settings-schema.js');
const settings = createDefaultSettings();
settings.host.hardwareAcceleration = false;
settings.extensions[manifest.id] = { enabled: true, settings: { paused: true } };
fs.writeFileSync(path.join(profile, 'settings.json'), JSON.stringify(settings, null, 2));
let app;
(async () => {
  try {
    const env = { ...process.env, APPDOCK_WALLPAPER_FIXTURE: '1' };
    delete env.ELECTRON_RUN_AS_NODE;
    app = await electron.launch({
      executablePath: process.env.APPDOCK_TEST_EXE || path.join(host, 'publish/win-unpacked/AppDock.at365.exe'),
      args: [`--test-profile=${profile}`], env, timeout: 30000,
    });
    const page = await app.firstWindow();
    await page.getByRole('heading', { name: 'ホーム', exact: true }).waitFor();
    await page.getByRole('button', { name: 'Applet', exact: true }).click();
    await page.getByRole('complementary', { name: 'Applet一覧' }).getByRole('button', { name: /WallpaperSlideshow.at365/ }).click();
    await page.getByRole('button', { name: '設定を開く', exact: true }).click();
    const apply = page.getByRole('button', { name: 'Windowsの背景を「画像・スパン」に設定', exact: true });
    await apply.waitFor();
    await page.getByLabel('壁紙の更新間隔（秒）', { exact: true }).fill('77');
    assert.match(await page.locator('.applet-settings').innerText(), /手動では Windows.*「個人用設定」.*「画像」.*「スパン」/);
    const firstClick = apply.click();
    const pending = page.getByRole('button', { name: '実行中…', exact: true });
    await pending.waitFor();
    assert(await pending.isDisabled());
    assert(await page.getByRole('button', { name: 'Windowsの背景設定を開く', exact: true }).isDisabled());
    await firstClick;
    await page.getByRole('status').filter({ hasText: 'Windowsの背景を「画像・スパン」に設定しました。' }).waitFor();
    assert.equal(await page.getByLabel('壁紙の更新間隔（秒）', { exact: true }).inputValue(), '77');
    let snapshot = await page.evaluate(() => window.dock.snapshot());
    assert.equal(snapshot.settings.value.extensions[manifest.id].settings.paused, true);
    assert.equal(snapshot.settings.value.extensions[manifest.id].settings.intervalSeconds, undefined);
    await page.screenshot({ path: path.join(profile, 'success.png') });
    await app.evaluate(({ BrowserWindow }) => BrowserWindow.getAllWindows()[0].setBounds({ width: 900, height: 720 }));
    await page.screenshot({ path: path.join(profile, 'small.png') });
    assert(await page.locator('.applet-settings').evaluate((element) => element.scrollWidth <= element.clientWidth + 1));
    await apply.click();
    await page.getByRole('alert').filter({ hasText: 'fixture background failure' }).waitFor();
    assert.equal(await page.getByLabel('壁紙の更新間隔（秒）', { exact: true }).inputValue(), '77');
    await page.getByRole('button', { name: 'Windowsの背景設定を開く', exact: true }).click();
    await page.getByRole('status').filter({ hasText: 'Windowsの背景設定を開きました。' }).waitFor();
    await page.getByRole('button', { name: '変更をすべて保存', exact: true }).click();
    await page.getByRole('status').filter({ hasText: '設定を保存' }).waitFor();
    snapshot = await page.evaluate(() => window.dock.snapshot());
    assert.deepEqual(snapshot.settings.value.extensions[manifest.id].settings, { paused: true, intervalSeconds: 77 });
    await page.getByRole('button', { name: 'Appletに戻る', exact: true }).click();
    await page.getByRole('switch', { name: 'WallpaperSlideshow.at365を有効にする', exact: true }).click();
    await page.getByRole('button', { name: '設定を開く', exact: true }).click();
    await apply.waitFor();
    await page.waitForFunction(() => document.querySelector('button[aria-describedby="setting-action-help-at365.wallpaper-slideshow.prepare-background"]')?.disabled);
    assert(await apply.isDisabled());
    await page.screenshot({ path: path.join(profile, 'stopped.png') });
    fs.writeFileSync(path.join(profile, 'result.json'), JSON.stringify({ ok: true, version: snapshot.version,
      checks: ['manual instructions', 'native command success / failure', 'in-flight buttons disabled', 'pause and unsaved draft preserved', 'background settings command', 'no action data persisted', 'stopped buttons disabled'] }, null, 2));
    console.log(profile);
  } finally { if (app) await app.close(); }
})().catch((error) => { console.error(error); process.exitCode = 1; });
