const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
const host = path.resolve(root, '../AppDock.at365');
const { _electron: electron } = require(path.join(host, 'node_modules/playwright'));
const profile = path.join(root, '.artifacts', `ui-${Date.now()}`);
const folder = path.join(profile, 'extensions', 'wallpaper');
const fixture = path.join(root, 'Applet.WallpaperSlideshow.RegressionTests/bin/Release/net10.0-windows');
fs.mkdirSync(folder, { recursive: true });
for (const file of fs.readdirSync(fixture)) if (fs.statSync(path.join(fixture, file)).isFile()) fs.copyFileSync(path.join(fixture, file), path.join(folder, file));
const manifest = JSON.parse(fs.readFileSync(path.join(root, 'extension.json')));
manifest.entry = 'Applet.WallpaperSlideshow.RegressionTests.exe';
fs.writeFileSync(path.join(folder, 'extension.json'), JSON.stringify(manifest));
const incompatible = path.join(profile, 'extensions', 'zz-incompatible');
fs.mkdirSync(incompatible);
fs.copyFileSync(path.join(fixture, manifest.entry), path.join(incompatible, manifest.entry));
fs.writeFileSync(path.join(incompatible, 'extension.json'), JSON.stringify({ ...manifest, commands: undefined, settingActions: undefined, id: 'test.requires-new-host', name: 'Test.Future.Applet', minimumHostVersion: '99.0.0' }));
const settings = require(path.join(host, 'out/main/shared/settings-schema.js')).createDefaultSettings();
settings.host.notifications = false;
settings.globalShortcutCommands = [];
settings.extensions[manifest.id] = { enabled: false, settings: { monitors: JSON.stringify([
  { Folder: 'C:/Example/A', Mode: 4, TileCount: 13, PaddingLeft: 1, PaddingRight: 2, PaddingTop: 3, PaddingBottom: 40 },
  { Folder: 'C:/Example/C', Mode: 1, PaddingBottom: 5 },
]), thumbnailWidth: 1000, thumbnailHeight: 1000 } };
fs.writeFileSync(path.join(profile, 'settings.json'), JSON.stringify(settings));
const env = { ...process.env, APPDOCK_WALLPAPER_FIXTURE: '1' };
delete env.ELECTRON_RUN_AS_NODE;

(async () => {
  const app = await electron.launch({ executablePath: process.argv[2] ? path.resolve(process.argv[2]) : require(path.join(host, 'node_modules/electron')),
    args: [...(process.argv[2] ? [] : [host]), `--test-profile=${profile}`], env });
  const page = await app.firstWindow(); page.setDefaultTimeout(12000);
  const errors = []; page.on('pageerror', (error) => errors.push(error.message));
  try {
    await page.getByRole('heading', { name: 'ホーム', exact: true }).waitFor();
    await page.evaluate(() => window.dock.toggleExtension('test.requires-new-host', true));
    const incompatibleState = await page.evaluate(() => window.dock.snapshot());
    const future = incompatibleState.extensions.find((e) => e.id === 'test.requires-new-host');
    assert.equal(future.state, 'error'); assert.match(future.error, /99.0.0/); assert.equal(future.scheduledStartAt, undefined);
    await page.evaluate(() => window.dock.toggleExtension('test.requires-new-host', false));
    await page.getByRole('button', { name: 'Applet', exact: true }).click();
    await page.getByRole('button', { name: 'WallpaperSlideshow.at365', exact: false }).first().click();
    await page.getByRole('tab', { name: '設定', exact: true }).click();
    assert.equal(await page.getByRole('spinbutton', { name: '開始までの秒数' }).inputValue(), '30');
    assert.equal(await page.getByRole('textbox', { name: 'モニター1 画像ソースフォルダー 1', exact: true }).inputValue(), 'C:/Example/A');
    assert.equal(await page.getByRole('combobox', { name: 'モニター1 表示モード', exact: true }).inputValue(), 'Tile');
    assert.equal(await page.getByRole('spinbutton', { name: 'モニター1 下のPadding（px）', exact: true }).inputValue(), '40');
    assert.equal(await page.getByRole('spinbutton', { name: 'モニター2 下のPadding（px）', exact: true }).inputValue(), '5');
    await page.getByRole('button', { name: 'モニター1 画像ソースフォルダーを追加', exact: true }).click();
    await page.getByRole('textbox', { name: 'モニター1 画像ソースフォルダー 2', exact: true }).fill('C:/Example/B');
    await app.evaluate(({ dialog }) => { dialog.showOpenDialog = async () => ({ canceled: false, filePaths: ['C:/Example/Chosen'] }); });
    await page.getByRole('button', { name: 'モニター2 画像ソースフォルダー 1を選ぶ', exact: true }).click();
    await page.getByRole('textbox', { name: 'モニター2 画像ソースフォルダー 1', exact: true }).fill('C:/Example/C');
    await page.getByRole('button', { name: 'モニターを追加', exact: true }).click();
    await page.getByRole('spinbutton', { name: 'モニター3 下のPadding（px）', exact: true }).fill('9');
    await page.getByRole('button', { name: 'モニター3を前へ移動', exact: true }).click();
    assert.equal(await page.getByRole('spinbutton', { name: 'モニター2 下のPadding（px）', exact: true }).inputValue(), '9');
    await page.getByRole('button', { name: 'モニター2の設定を削除', exact: true }).click();
    for (const width of [1280, 900]) {
      await page.setViewportSize({ width, height: 840 });
      assert(await page.locator('main').evaluate(element => element.scrollWidth <= element.clientWidth));
      await page.screenshot({ path: path.join(profile, `monitor-settings-${width}.png`) });
    }
    await page.getByRole('button', { name: '変更をすべて保存', exact: true }).click();
    await page.waitForFunction((id) => window.dock.snapshot().then((s) => s.settings.value.extensions[id].settings.monitors?.[0]?.Folders.includes('C:/Example/B')), manifest.id);
    let saved = await page.evaluate(id => window.dock.snapshot().then(s => s.settings.value.extensions[id].settings), manifest.id);
    assert.equal(saved.monitors[0].PaddingBottom, 40); assert.equal(saved.monitors[0].TileCount, 13);
    assert.equal(saved.monitors[1].PaddingBottom, 5); assert.equal(saved.monitors[0].Folder, undefined);
    // Catalog and shortcuts exist even while disabled; aliases do not create duplicate new choices.
    await page.getByRole('button', { name: 'コマンドを検索', exact: false }).first().click();
    await page.getByRole('combobox', { name: 'コマンドを検索' }).fill('at365.wallpaper-slideshow.settings.paused.off');
    assert.equal(await page.locator('.palette-execute').count(), 1);
    assert.equal(await page.locator('.palette-execute').isEnabled(), false);
    await page.keyboard.press('Escape');
    // Start and cancel a real host timer; this fixture never changes the Windows wallpaper.
    await page.evaluate((id) => window.dock.toggleExtension(id, true), manifest.id);
    let snapshot = await page.evaluate(() => window.dock.snapshot());
    assert.equal(snapshot.extensions[0].state, 'waiting');
    await page.getByRole('tab', { name: '説明', exact: true }).click();
    await page.getByRole('button', { name: '今すぐ開始', exact: true }).waitFor();
    await page.screenshot({ path: path.join(profile, 'waiting.png') });
    await page.evaluate((id) => window.dock.toggleExtension(id, false), manifest.id);
    snapshot = await page.evaluate(() => window.dock.snapshot()); assert.equal(snapshot.extensions[0].state, 'stopped');
    await page.evaluate((id) => window.dock.toggleExtension(id, true), manifest.id);
    await page.getByRole('button', { name: '今すぐ開始', exact: true }).click();
    await page.getByRole('button', { name: '最近使った壁紙', exact: true }).click();
    await page.locator('.panel-images img').first().waitFor();
    assert.equal(await page.locator('.panel-images img').count(), 4);
    await page.waitForFunction(() => [...document.querySelectorAll('.panel-images img')].every(image => image.complete && image.naturalWidth > 0));
    assert(await page.locator('.panel-images img').evaluateAll((images) => images.every((image) => image.complete && image.naturalWidth > 0)));
    await page.getByRole('button', { name: 'モニター2', exact: true }).click();
    await page.waitForFunction(() => document.querySelectorAll('.panel-images article').length === 1);
    await page.getByRole('button', { name: 'モニター1', exact: true }).click();
    await page.waitForFunction(() => document.querySelectorAll('.panel-images article').length === 4);
    await page.locator('.image-preview-button').first().click();
    await page.getByRole('dialog', { name: '壁紙のプレビュー' }).waitFor();
    await page.getByRole('dialog', { name: '壁紙のプレビュー' }).getByRole('button', { name: '閉じる', exact: true }).click();
    for (const size of [{ width: 1280, height: 840 }, { width: 900, height: 620 }]) {
      await page.setViewportSize(size);
      assert(await page.locator('main').evaluate((element) => element.scrollWidth <= element.clientWidth));
      await page.screenshot({ path: path.join(profile, `history-${size.width}.png`) });
    }
    await page.getByRole('button', { name: '次のページ', exact: true }).click();
    await page.waitForFunction(() => document.querySelectorAll('.panel-images img').length === 3);
    assert.equal(await page.locator('.panel-images img').count(), 3);
    await page.waitForFunction(() => [...document.querySelectorAll('.panel-images img')].some(image => image.naturalWidth === 1000));
    await page.getByRole('button', { name: '画像を削除', exact: true }).first().click();
    await page.getByRole('button', { name: 'キャンセル', exact: true }).click();
    await page.waitForFunction(() => document.querySelectorAll('.panel-images article').length === 3);
    await page.getByRole('button', { name: '画像を削除', exact: true }).first().click();
    await page.getByRole('button', { name: 'ごみ箱へ移す', exact: true }).click();
    await page.waitForFunction(() => document.querySelectorAll('.panel-images article').length === 2);
    await page.getByRole('tab', { name: '設定', exact: true }).click();
    await page.getByRole('spinbutton', { name: '最近使った壁紙の1ページの表示件数', exact: true }).fill('6');
    await page.getByRole('button', { name: '変更をすべて保存', exact: true }).click();
    await page.getByRole('tab', { name: '説明', exact: true }).click();
    await page.waitForFunction(() => document.querySelectorAll('.panel-images article').length === 6);
    await page.getByRole('button', { name: '壁紙の操作に戻る', exact: true }).click();
    await page.waitForFunction(() => document.querySelectorAll('.panel-images img').length === 0);
    assert.equal(await page.locator('.panel-images img').count(), 0);
    await page.getByRole('button', { name: '停止', exact: true }).click();
    await page.waitForFunction((id) => window.dock.snapshot().then((s) => s.settings.value.extensions[id].settings.paused === true), manifest.id);
    await page.getByRole('button', { name: '開始／再開', exact: true }).click();
    await page.waitForFunction((id) => window.dock.snapshot().then((s) => s.settings.value.extensions[id].settings.paused === false), manifest.id);
    await page.getByRole('button', { name: '次の壁紙に更新', exact: true }).click();
    await page.getByRole('button', { name: '開始／停止を切り替え', exact: true }).click();
    await page.waitForFunction((id) => window.dock.snapshot().then((s) => s.settings.value.extensions[id].settings.paused === true), manifest.id);
    snapshot = await page.evaluate(() => window.dock.snapshot());
    assert.equal(snapshot.extensions[0].tray.length, 0);
    assert.equal(snapshot.extensions[0].state, 'running');
    await page.screenshot({ path: path.join(profile, 'playback-controls.png') });
    // Update-feed regression belongs to the host portable-updates tests.
    const expectedMissing = entry => entry.source === manifest.id && entry.message.startsWith('[画像サイズ: ') && entry.message.includes('missing-image.png');
    assert.equal(snapshot.logs.filter(expectedMissing).length, 1, 'fixture missing image logged once');
    assert.deepEqual(snapshot.logs.filter((entry) => entry.level === 'error' && entry.source !== 'test.requires-new-host' && !expectedMissing(entry)), []);
    assert.deepEqual(errors, []);
    fs.writeFileSync(path.join(profile, 'result.json'), JSON.stringify({ok:true,version:snapshot.version,checks:['generated settings commands / panel / persistence / history / local images']}));
    console.log(JSON.stringify({ ok: true, profile, checks: ['legacy JSON to per-monitor form / independent padding / folder picker / reorder / persistence', 'generated command catalog / explicit activation / wait cancellation', 'per-monitor history / page size form / local large images / preview / responsive layout', 'image deletion / cancel / refresh / no per-item commands', 'pause persistence / no tray / no page errors'] }, null, 2));
  } finally { await app.close(); }
})().catch((error) => { console.error(error); process.exitCode = 1; });
