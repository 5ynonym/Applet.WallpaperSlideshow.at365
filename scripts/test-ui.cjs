const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
const host = path.resolve(root, '../AppDock.at365');
const { _electron: electron } = require(path.join(host, 'node_modules/playwright'));
const profile = path.join(root, 'artifacts', `ui-${Date.now()}`);
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
fs.writeFileSync(path.join(incompatible, 'extension.json'), JSON.stringify({ ...manifest, id: 'test.requires-new-host', name: 'Test.Future.Applet', minimumHostVersion: '99.0.0' }));
const settings = require(path.join(host, 'out/main/shared/settings-schema.js')).createDefaultSettings();
settings.host.notifications = false;
settings.globalShortcutCommands = [];
settings.extensions[manifest.id] = { enabled: false, settings: {} };
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
    await page.getByRole('button', { name: 'Applet.WallpaperSlideshow.at365', exact: false }).first().click();
    await page.getByRole('button', { name: '設定を開く', exact: true }).click();
    assert.equal(await page.getByRole('spinbutton', { name: '開始までの秒数' }).inputValue(), '30');
    await page.getByRole('textbox', { name: 'モニターごとの画像設定' }).fill('[{"Folders":["C:/Example/A","C:/Example/B"],"Mode":"Tile","TileCount":4}]');
    await page.getByRole('button', { name: '変更をすべて保存', exact: true }).click();
    await page.waitForFunction((id) => window.dock.snapshot().then((s) => s.settings.value.extensions[id].settings.monitors?.includes('C:/Example/B')), manifest.id);
    // Start and cancel a real host timer; this fixture never changes the Windows wallpaper.
    await page.evaluate((id) => window.dock.toggleExtension(id, true), manifest.id);
    let snapshot = await page.evaluate(() => window.dock.snapshot());
    assert.equal(snapshot.extensions[0].state, 'waiting');
    await page.getByRole('button', { name: 'Applet', exact: true }).click();
    await page.getByRole('button', { name: '今すぐ開始', exact: true }).waitFor();
    await page.screenshot({ path: path.join(profile, 'waiting.png') });
    await page.evaluate((id) => window.dock.toggleExtension(id, false), manifest.id);
    snapshot = await page.evaluate(() => window.dock.snapshot()); assert.equal(snapshot.extensions[0].state, 'stopped');
    await page.evaluate((id) => window.dock.toggleExtension(id, true), manifest.id);
    await page.getByRole('button', { name: '今すぐ開始', exact: true }).click();
    await page.getByRole('button', { name: '最近使った壁紙', exact: true }).click();
    await page.locator('.panel-images img').first().waitFor();
    assert.equal(await page.locator('.panel-images img').count(), 4);
    assert(await page.locator('.panel-images img').evaluateAll((images) => images.every((image) => image.complete && image.naturalWidth > 0)));
    for (const size of [{ width: 1280, height: 840 }, { width: 900, height: 620 }]) {
      await page.setViewportSize(size);
      assert(await page.locator('main').evaluate((element) => element.scrollWidth <= element.clientWidth));
      await page.screenshot({ path: path.join(profile, `history-${size.width}.png`) });
    }
    await page.getByRole('button', { name: '次のページ', exact: true }).click();
    await page.waitForFunction(() => document.querySelectorAll('.panel-images img').length === 3);
    assert.equal(await page.locator('.panel-images img').count(), 3);
    await page.getByRole('button', { name: '壁紙の操作に戻る', exact: true }).click();
    await page.waitForFunction(() => document.querySelectorAll('.panel-images img').length === 0);
    assert.equal(await page.locator('.panel-images img').count(), 0);
    await page.getByRole('button', { name: '停止', exact: true }).click();
    await page.waitForFunction((id) => window.dock.snapshot().then((s) => s.settings.value.extensions[id].settings.paused === true), manifest.id);
    await page.getByRole('button', { name: '開始', exact: true }).click();
    await page.waitForFunction((id) => window.dock.snapshot().then((s) => s.settings.value.extensions[id].settings.paused === false), manifest.id);
    await page.getByRole('button', { name: '次の壁紙に更新', exact: true }).click();
    await page.getByRole('button', { name: '開始／停止を切り替え', exact: true }).click();
    await page.waitForFunction((id) => window.dock.snapshot().then((s) => s.settings.value.extensions[id].settings.paused === true), manifest.id);
    snapshot = await page.evaluate(() => window.dock.snapshot());
    assert.equal(snapshot.extensions[0].tray.length, 0);
    assert.equal(snapshot.extensions[0].state, 'running');
    await page.screenshot({ path: path.join(profile, 'playback-controls.png') });
    await app.evaluate(({ net, shell }) => {
      net.fetch = async (url) => {
        globalThis.testUpdateUrl = url;
        return new Response(JSON.stringify({ tag_name: 'v0.6.0', html_url: 'https://untrusted.example/' }));
      };
      shell.openExternal = async (url) => { globalThis.testReleaseUrl = url; };
    });
    const checks = page.getByRole('button', { name: '更新を確認', exact: true });
    assert.equal(await checks.count(), 2);
    await checks.first().click();
    await page.getByText('v0.6.0 が公開されています。', { exact: true }).waitFor();
    assert.equal(await app.evaluate(() => globalThis.testUpdateUrl), 'https://api.github.com/repos/5ynonym/Applet.WallpaperSlideshow.at365/releases/latest');
    await page.getByRole('button', { name: 'リリースを開く', exact: true }).click();
    assert.equal(await app.evaluate(() => globalThis.testReleaseUrl), 'https://github.com/5ynonym/Applet.WallpaperSlideshow.at365/releases');
    await checks.last().click();
    await page.waitForFunction(() => document.querySelectorAll('.version-check [role="status"]').length === 2);
    assert.equal(await app.evaluate(() => globalThis.testUpdateUrl), 'https://api.github.com/repos/5ynonym/AppDock.at365/releases/latest');
    assert.deepEqual(snapshot.logs.filter((entry) => entry.level === 'error' && entry.source !== 'test.requires-new-host'), []);
    assert.deepEqual(errors, []);
    console.log(JSON.stringify({ ok: true, profile, checks: ['30-second default / JSON editing / persistence', 'waiting / disable / immediate start', 'AppDock history / images / paging / responsive layout', 'pause persistence / no tray / no page errors', 'host / Applet updates / fixed release URLs (mock network)'] }, null, 2));
  } finally { await app.close(); }
})().catch((error) => { console.error(error); process.exitCode = 1; });
