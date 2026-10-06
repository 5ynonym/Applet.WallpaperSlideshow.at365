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
  let panel, logged = false, diagnostics = '';
  const child = spawn(fixture, ['--protocol-fixture'], { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
  child.stderr.on('data', (data) => { diagnostics += data; });
  const peer = new JsonLinePeer(child.stdout, child.stdin, async (method, params) => {
    if (method === 'host.ui.panel') { panel = params; return null; }
    if (method === 'host.settings.set') { settings[params.key] = params.value; return null; }
    if (method === 'host.log') { logged = true; return null; }
    throw new Error('Unexpected API ' + method);
  });
  const exited = new Promise((resolve, reject) => { child.on('error', reject); child.on('exit', (code) => resolve(code)); });
  let deactivated = false;
  try {
    const result = await peer.request('activate', { id: 'at365.wallpaper-slideshow', settings });
    assert.equal(result.tray.length, 0);
    assert.equal(result.commands.length, 13);
    assert.equal(panel.facts.find((fact) => fact.label === '状態').value, '再生中');
    await peer.request('command.execute', { id: 'at365.wallpaper-slideshow.pause' });
    assert.equal(settings.paused, true);
    assert.match(panel.facts.find((fact) => fact.label === '状態').value, /一時停止/);
    await peer.request('command.execute', { id: 'at365.wallpaper-slideshow.resume' });
    assert.equal(settings.paused, false);
    await peer.request('settings.changed', { intervalSeconds: 7 });
    assert.equal(panel.facts.find((fact) => fact.label === '更新間隔').value, '7 秒');
    await peer.request('settings.changed', { intervalSeconds: 0 });
    assert(logged); assert.match(panel.description, /設定エラー/);
    assert.equal(panel.facts.find((fact) => fact.label === '更新間隔').value, '7 秒');
    await peer.request('command.execute', { id: 'at365.wallpaper-slideshow.history' });
    assert.equal(panel.images.length, 4); assert(panel.images.every((image) => image.image.startsWith('data:image/jpeg;base64,')));
    assert.match(panel.images[0].title, /image6/);
    await peer.request('command.execute', { id: 'at365.wallpaper-slideshow.history.next' });
    assert.equal(panel.images.length, 3); assert.match(panel.images[0].title, /image2/);
    await peer.request('command.execute', { id: 'at365.wallpaper-slideshow.home' });
    assert.equal(panel.images, null);
    await peer.request('deactivate'); deactivated = true;
    child.stdin.end();
    assert.equal(await exited, 0, diagnostics);
    console.log(JSON.stringify({ ok: true, checks: ['activation / no tray', 'pause persistence / resume', 'live settings / invalid rollback', 'paged history / thumbnails / release', 'deactivate / EOF'] }, null, 2));
  } finally {
    if (!deactivated && !peer.closed) await peer.request('deactivate').catch(() => {});
    child.stdin.end(); peer.close();
    if (child.exitCode === null) child.kill();
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
