const { spawn } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
const host = path.resolve(root, '../AppDock.at365');
const { JsonLinePeer } = require(path.join(host, 'out/main/main/core/rpc.js'));
const fixture = path.join(root, 'Applet.WallpaperSlideshow.RegressionTests/bin/Release/net10.0-windows/Applet.WallpaperSlideshow.RegressionTests.exe');

(async () => {
  const settings = {};
  const imageDirectory = fs.mkdtempSync(path.join(root, 'artifacts', 'protocol-images-'));
  let panel, logged = false, diagnostics = '';
  const child = spawn(fixture, ['--protocol-fixture'], { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
  child.stderr.on('data', (data) => { diagnostics += data; });
  const peer = new JsonLinePeer(child.stdout, child.stdin, async (method, params) => {
    if (method === 'host.ui.panel') { panel = params; return null; }
    if (method === 'host.ui.imageDirectory') return imageDirectory;
    if (method === 'host.settings.set') { settings[params.key] = params.value; return null; }
    if (method === 'host.log') { logged = true; return null; }
    throw new Error('Unexpected API ' + method);
  });
  const exited = new Promise((resolve, reject) => { child.on('error', reject); child.on('exit', (code) => resolve(code)); });
  let deactivated = false;
  const action = (title, slot) => (slot === undefined ? [...panel.actions ?? [], ...panel.tabs ?? []] : panel.images[slot].actions).find(item => item.title === title).actionId;
  const click = (title, slot) => peer.request('panel.action', { id: action(title, slot) });
  try {
    const result = await peer.request('activate', { id: 'at365.wallpaper-slideshow', settings });
    assert.equal(result.tray.length, 0);
    assert.equal(result.commands.length, 11);
    assert.equal(result.commands.some(command => command.id.endsWith('.resume')), false);
    for (const [suffix, title] of [['start', '壁紙スライドショーを開始／再開'], ['stop', '壁紙スライドショーを停止'],
      ['toggle', '壁紙スライドショーの開始／停止を切り替え'], ['next', '次の壁紙に更新']]) {
      assert.equal(result.commands.find((command) => command.id === 'at365.wallpaper-slideshow.' + suffix)?.title, title);
    }
    assert.deepEqual(panel.actions.slice(0, 4).map((action) => action.title), ['開始／再開', '停止', '開始／停止を切り替え', '次の壁紙に更新']);
    assert.equal(panel.facts.find((fact) => fact.label === '状態').value, '再生中');
    await peer.request('command.execute', { id: 'at365.wallpaper-slideshow.stop' });
    assert.equal(settings.paused, true);
    await peer.request('command.execute', { id: 'at365.wallpaper-slideshow.prepare-background' });
    assert.equal(settings.paused, true, 'background preparation preserves manual pause');
    await assert.rejects(peer.request('command.execute', { id: 'at365.wallpaper-slideshow.prepare-background' }), /fixture background failure/);
    assert.equal(settings.paused, true, 'failed background preparation preserves manual pause');
    await peer.request('command.execute', { id: 'at365.wallpaper-slideshow.background-settings' });
    assert.equal(settings.paused, true);
    await peer.request('command.execute', { id: 'at365.wallpaper-slideshow.next' });
    assert.equal(diagnostics.includes('FIXTURE_NEXT_WALLPAPER'), false);
    await peer.request('command.execute', { id: 'at365.wallpaper-slideshow.start' });
    assert.equal(settings.paused, false);
    await peer.request('command.execute', { id: 'at365.wallpaper-slideshow.next' });
    for (let attempt = 0; attempt < 50 && !diagnostics.includes('FIXTURE_NEXT_WALLPAPER'); attempt++)
      await new Promise((resolve) => setTimeout(resolve, 10));
    assert.equal(diagnostics.split('FIXTURE_NEXT_WALLPAPER').length - 1, 1);
    await peer.request('command.execute', { id: 'at365.wallpaper-slideshow.toggle' });
    assert.equal(settings.paused, true);
    await peer.request('command.execute', { id: 'at365.wallpaper-slideshow.toggle' });
    assert.equal(settings.paused, false);
    await peer.request('command.execute', { id: 'at365.wallpaper-slideshow.stop' });
    assert.equal(settings.paused, true);
    assert.match(panel.facts.find((fact) => fact.label === '状態').value, /停止中/);
    await peer.request('command.execute', { id: 'at365.wallpaper-slideshow.start' });
    assert.equal(settings.paused, false);
    await peer.request('settings.changed', { intervalSeconds: 7 });
    assert.equal(panel.facts.find((fact) => fact.label === '更新間隔').value, '7 秒');
    await peer.request('settings.changed', { intervalSeconds: 0 });
    assert(logged); assert.match(panel.description, /設定エラー/);
    assert.equal(panel.facts.find((fact) => fact.label === '更新間隔').value, '7 秒');
    await peer.request('command.execute', { id: 'at365.wallpaper-slideshow.stop' });
    assert.equal(settings.paused, true);
    assert.match(panel.facts.find((fact) => fact.label === '状態').value, /停止中/);
    await peer.request('command.execute', { id: 'at365.wallpaper-slideshow.start' });
    assert.equal(settings.paused, false);
    assert.equal(panel.facts.find((fact) => fact.label === '状態').value, '再生中');
    await peer.request('command.execute', { id: 'at365.wallpaper-slideshow.history' });
    assert.equal(panel.images.length, 4); assert(panel.images.every(image => fs.existsSync(image.imageFile)));
    assert.match(panel.images[0].title, /image6/);
    const firstFiles = panel.images.map(image => image.imageFile);
    const staleAction = action('画像を削除', 0);
    await click('モニター2');
    assert.equal(panel.images.length, 1); assert.match(panel.description, /モニター2/); assert.match(panel.images[0].title, /image3/);
    assert(firstFiles.every(file => !fs.existsSync(file)), 'previous monitor thumbnails released');
    await assert.rejects(peer.request('panel.action', { id: staleAction }), /更新/);
    await click('モニター1');
    await peer.request('command.execute', { id: 'at365.wallpaper-slideshow.history.next' });
    assert.equal(panel.images.length, 3); assert.match(panel.images[0].title, /image2/);
    assert(fs.statSync(panel.images.at(-1).imageFile).size > 200000, 'local thumbnail keeps image quality without byte caps');
    assert.deepEqual(panel.images[0].actions.map(action => action.title), ['画像を開く', '画像を削除']);
    const victim = panel.images[0].tooltip;
    await click('画像を削除', 0);
    assert.equal(panel.title, '画像を削除'); assert.equal(panel.facts[0].value, victim); assert(fs.existsSync(victim));
    await click('キャンセル'); assert(fs.existsSync(victim));
    await click('画像を削除', 0);
    const confirmedAction = action('ごみ箱へ移す');
    await click('ごみ箱へ移す');
    assert.equal(fs.existsSync(victim), false); assert.equal(panel.images.length, 2); assert.match(panel.images[0].title, /image1/);
    await assert.rejects(peer.request('panel.action', { id: confirmedAction }), /更新/);
    await peer.request('settings.changed', { intervalSeconds: 7, historyPageSize: 2 });
    assert.equal(panel.images.length, 2);
    await peer.request('command.execute', { id: 'at365.wallpaper-slideshow.history' }); assert.equal(panel.images.length, 2);
    await peer.request('settings.changed', { intervalSeconds: 7, historyPageSize: 16 });
    assert.equal(panel.images.length, 6);
    await click('モニター2'); assert.equal(panel.images.length, 1);
    await click('モニター1'); assert.equal(panel.images.length, 6);
    await peer.request('command.execute', { id: 'at365.wallpaper-slideshow.home' });
    assert.equal(panel.images, null);
    await peer.request('deactivate'); deactivated = true;
    child.stdin.end();
    assert.equal(await exited, 0, diagnostics);
    console.log(JSON.stringify({ ok: true, checks: ['activation / no tray / unified start / command registration', 'start / stop / toggle / next', 'playback with invalid image settings', 'live settings / invalid rollback', 'history page sizes 2 / 4 / 16 / thumbnails / release', 'delete confirmation / cancel / exact fixture file removed / repeat rejected', 'deactivate / EOF'] }, null, 2));
  } finally {
    if (!deactivated && !peer.closed) await peer.request('deactivate').catch(() => {});
    child.stdin.end(); peer.close();
    if (child.exitCode === null) child.kill();
    fs.rmSync(imageDirectory, { recursive: true, force: true });
  }
})().catch((error) => { console.error(error); process.exitCode = 1; });

// Exercise the published native entry point, but reject settings before any desktop mutation.
(async () => {
  const exe = path.join(root, 'publish/Applet.WallpaperSlideshow.at365/Applet.WallpaperSlideshow.at365.exe');
  if (!fs.existsSync(exe)) throw new Error('Publish the Applet before the native startup smoke.');
  const child = spawn(exe, [], { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
  let diagnostics = ''; child.stderr.on('data', (data) => { diagnostics += data; });
  const peer = new JsonLinePeer(child.stdout, child.stdin, async () => null);
  const exited = new Promise((resolve, reject) => { child.on('error', reject); child.on('exit', resolve); });
  try {
    await assert.rejects(peer.request('activate', { id: 'at365.wallpaper-slideshow', settings: { monitors: '{' } }), /JSON|depth|Depth|invalid|Invalid|expected|Expected/i);
    await peer.request('deactivate'); child.stdin.end();
    assert.equal(await exited, 0, diagnostics);
    console.log('PASS published native startup / invalid settings rejected before desktop access / clean exit');
  } finally { child.stdin.end(); peer.close(); if (child.exitCode === null) child.kill(); }
})().catch((error) => { console.error(error); process.exitCode = 1; });
