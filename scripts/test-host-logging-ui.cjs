// Isolated native logging test. The real Applet rejects settings before desktop access.
const fs = require('node:fs');
const path = require('node:path');
const net = require('node:net');
const { spawn } = require('node:child_process');
const { createHash } = require('node:crypto');
const { createRequire } = require('node:module');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
const host = path.resolve(root, '../AppDock.at365');
const requireHost = createRequire(path.join(host, 'package.json'));
const { chromium } = requireHost('playwright');
const output = path.join(root, '.artifacts', `host-logging-${Date.now()}`);
const portable = path.join(host, 'publish/AppDock.at365.exe');
const hash = file => createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const checks = [];
fs.mkdirSync(output, { recursive: true });
async function until(check, message) {
  const end = Date.now() + 30000;
  while (Date.now() < end) {
    if (await check()) return;
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  throw Error(message);
}
async function run(fixture) {
  const base = path.join(output, fixture ? 'fixture' : 'published');
  const extension = path.join(base, 'extensions/wallpaper');
  fs.mkdirSync(extension, { recursive: true });
  fs.cpSync(path.join(root, fixture ? 'Applet.WallpaperSlideshow.RegressionTests/bin/Release/net10.0-windows' : 'publish/Applet.WallpaperSlideshow.at365'), extension, { recursive: true });
  const manifest = JSON.parse(fs.readFileSync(path.join(root, 'extension.json'), 'utf8'));
  if (fixture) manifest.entry = 'Applet.WallpaperSlideshow.RegressionTests.exe';
  manifest.startupDelaySeconds = 0;
  fs.writeFileSync(path.join(extension, 'extension.json'), JSON.stringify(manifest));
  const settings = requireHost('./out/main/shared/settings-schema.js').createDefaultSettings();
  settings.host.hardwareAcceleration = false;
  settings.host.notifications = false;
  settings.keybindings = [];
  settings.gestures.enabled = false;
  settings.extensions[manifest.id] = { enabled: true, settings: fixture ? { paused: true } : { monitors: '{' } };
  fs.writeFileSync(path.join(base, 'settings.json'), JSON.stringify(settings));
  const executable = path.join(base, 'AppDock.at365.exe');
  fs.copyFileSync(portable, executable);
  assert.equal(hash(executable), hash(portable));
  const server = net.createServer();
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const port = server.address().port;
  await new Promise(resolve => server.close(resolve));
  const env = { ...process.env, LOCALAPPDATA: path.join(base, 'LocalAppData') };
  delete env.ELECTRON_RUN_AS_NODE;
  if (fixture) env.APPDOCK_WALLPAPER_FIXTURE = '1';
  else delete env.APPDOCK_WALLPAPER_FIXTURE;
  const child = spawn(executable, [`--test-profile=${base}`, `--remote-debugging-port=${port}`], { cwd: base, env, windowsHide: true, stdio: 'ignore' });
  let browser, page;
  try {
    await until(async () => {
      try { browser = await chromium.connectOverCDP(`http://127.0.0.1:${port}`, { timeout: 1000 }); return true; }
      catch { return false; }
    }, 'CDP connection');
    await until(() => { page = browser.contexts().flatMap(context => context.pages()).find(item => item.url().startsWith('appdock://host/')); return page; }, 'host page');
    const operation = fixture ? '[画像サイズ:' : '[Appletの開始]';
    await until(async () => (await page.evaluate(() => window.dock.snapshot())).logs.some(entry => entry.source === manifest.id && entry.level === 'error' && entry.message.includes(operation)), 'Applet error in snapshot');
    await page.getByRole('button', { name: 'ログを開く', exact: true }).click();
    await page.getByLabel('ログのApplet', { exact: true }).selectOption(manifest.id);
    await page.getByLabel('ログレベル', { exact: true }).selectOption('error');
    await page.locator('.log-row').filter({ hasText: operation }).waitFor();
    assert.equal(await page.locator('.log-row').filter({ hasText: operation }).count(), 1);
    await page.screenshot({ path: path.join(base, 'all-logs.png') });
    await page.getByRole('button', { name: 'Applet', exact: true }).click();
    await page.getByRole('complementary', { name: 'Applet一覧' }).getByRole('button', { name: /WallpaperSlideshow.at365/ }).click();
    await page.getByRole('tab', { name: 'ログ', exact: true }).click();
    await page.locator('.detail-logs .log-row').filter({ hasText: operation }).waitFor();
    await page.screenshot({ path: path.join(base, 'applet-logs.png') });
    const snapshot = await page.evaluate(() => window.dock.snapshot());
    fs.writeFileSync(path.join(base, 'logs-snapshot.json'), JSON.stringify(snapshot.logs, null, 2));
    checks.push(`${fixture ? 'fixture engine' : 'published native'}: host error / source / global logs / Applet logs / no duplicate`);
    await page.evaluate(() => window.dock.executeCommand('appdock.quit'));
    await until(() => child.exitCode !== null, 'normal exit');
    assert.equal(child.exitCode, 0);
    const files = [];
    function scan(folder) {
      for (const item of fs.readdirSync(folder, { withFileTypes: true })) {
        const file = path.join(folder, item.name);
        if (item.isDirectory()) scan(file); else files.push(file);
      }
    }
    scan(base);
    assert(!files.some(file => path.basename(file) === 'errors.log'));
    const logFiles = files.filter(file => /\.log$/.test(file));
    assert(logFiles.some(file => fs.readFileSync(file, 'utf8').includes(operation)), 'host log file contains Applet error');
    if (fixture) {
      const savedLogs = logFiles.filter(file => path.basename(file) === 'host.log')
        .flatMap(file => fs.readFileSync(file, 'utf8').trim().split('\n').map(line => JSON.parse(line)));
      assert(savedLogs.some(entry => entry.level === 'error' && entry.source === manifest.id && entry.message.includes('[fixture shutdown]')),
        'cleanup error retains its Applet ID and error level in the host file');
    }
    checks.push(`${fixture ? 'fixture' : 'published'}: clean exit / host file / no independent errors.log`);
    return { hostVersion: snapshot.version, appletVersion: manifest.version, exeHash: fixture ? null : hash(path.join(extension, manifest.entry)) };
  } finally {
    if (child.exitCode === null) {
      if (page) await page.evaluate(() => window.dock.executeCommand('appdock.quit')).catch(() => {});
      await until(() => child.exitCode !== null, 'cleanup exit').catch(() => child.kill());
    }
    if (browser) await browser.close();
  }
}
(async () => {
  await run(true);
  const published = await run(false);
  fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify({ ok: true, checks, published, hostHash: hash(portable) }, null, 2));
  console.log(JSON.stringify({ ok: true, output, checks }));
})().catch(error => {
  fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify({ ok: false, checks, error: error.stack }, null, 2));
  console.error(error); process.exitCode = 1;
});
