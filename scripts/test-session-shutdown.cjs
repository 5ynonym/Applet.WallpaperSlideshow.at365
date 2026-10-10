// Send session messages only to isolated fixture HWNDs. Never shut down Windows.
const { spawn, execFileSync } = require('node:child_process');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
const host = path.resolve(root, '../AppDock.at365');
const { JsonLinePeer } = require(path.join(host, 'out/main/main/core/rpc.js'));
const fixture = path.join(root, 'Applet.WallpaperSlideshow.RegressionTests/bin/Release/net10.0-windows/Applet.WallpaperSlideshow.RegressionTests.exe');
const output = path.join(root, '.artifacts', `session-shutdown-${Date.now()}`);
fs.mkdirSync(output, { recursive: true });
const checks = [];
const waitFor = async condition => {
  const end = Date.now() + 10000;
  while (!condition()) {
    if (Date.now() > end) throw Error('Timed out waiting for fixture diagnostic');
    await new Promise(resolve => setTimeout(resolve, 20));
  }
};
async function run(mode) {
  const imageDirectory = path.join(output, mode);
  fs.mkdirSync(imageDirectory);
  const child = spawn(fixture, ['--protocol-fixture'], { windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
  let diagnostics = '';
  child.stderr.on('data', data => { diagnostics += data; });
  const logs = [];
  const peer = new JsonLinePeer(child.stdout, child.stdin, async (method, params) => {
    if (method === 'host.ui.imageDirectory') return imageDirectory;
    if (method === 'host.ui.panel') return null;
    if (method === 'host.log') { logs.push(params); return null; }
    throw Error('Unexpected host API: ' + method);
  });
  const exited = new Promise((resolve, reject) => { child.on('exit', resolve); child.on('error', reject); });
  const count = marker => diagnostics.split(marker).length - 1;
  try {
    await peer.request('activate', { id: 'at365.wallpaper-slideshow', settings: {} });
    await waitFor(() => /FIXTURE_SESSION_WINDOW (\d+) (\d+)/.test(diagnostics));
    const [, pid, window] = diagnostics.match(/FIXTURE_SESSION_WINDOW (\d+) (\d+)/);
    assert.equal(+pid, child.pid);
    const send = action => execFileSync(fixture, ['--send-session-message', pid, window, action], { windowsHide: true, encoding: 'utf8' }).trim();
    assert.equal(send('query'), '1', 'shutdown query allowed');
    assert.equal(count('FIXTURE_CLEANUP_BLACK'), 0, 'query does not clear wallpaper');
    assert.equal(send('cancel'), '0');
    assert.equal(count('FIXTURE_CLEANUP_BLACK'), 0, 'cancelled shutdown preserves wallpaper');
    await peer.request('command.execute', { id: 'at365.wallpaper-slideshow.next' });
    await waitFor(() => count('FIXTURE_NEXT_WALLPAPER') === 1);
    if (mode !== 'ordinary') {
      assert.equal(send(mode), '0');
      await waitFor(() => count('FIXTURE_CLEANUP_BLACK') === 1);
      assert.equal(send(mode), '0');
      assert.equal(count('FIXTURE_CLEANUP_BLACK'), 1, 'duplicate OS messages clean up once');
    }
    await peer.request('deactivate');
    child.stdin.end();
    assert.equal(await exited, 0, diagnostics);
    assert.equal(count('FIXTURE_CLEANUP_BLACK'), 1, 'OS notification and host deactivation share one cleanup');
    assert(logs.some(entry => entry.level === 'error' && entry.message.includes('[fixture shutdown]')), 'cleanup diagnostic drained to host');
    fs.writeFileSync(path.join(imageDirectory, 'diagnostics.log'), diagnostics);
    fs.writeFileSync(path.join(imageDirectory, 'host-logs.json'), JSON.stringify(logs, null, 2));
    checks.push(`${mode}: actual hidden HWND / query / cancel and continued playback / black BMP before native return / once / common log / clean RPC exit`);
  } finally {
    child.stdin.end(); peer.close();
    if (child.exitCode === null) child.kill();
  }
}
(async () => {
  for (const mode of ['end', 'logoff', 'ordinary']) await run(mode);
  fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify({ ok: true, checks }, null, 2));
  console.log(JSON.stringify({ ok: true, output, checks }));
})().catch(error => {
  fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify({ ok: false, checks, error: error.stack }, null, 2));
  console.error(error); process.exitCode = 1;
});
