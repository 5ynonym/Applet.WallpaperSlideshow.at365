const fs = require("node:fs");
const path = require("node:path");
const assert = require("node:assert/strict");
const { createRequire } = require("node:module");
// Isolated host only. No user Codex config, model invocation, or OS command execution.
module.exports = async function checkSettings({
  host,
  profile,
  page,
  id,
  keys,
  changes,
  invalid,
  hidden,
  switches = [],
}) {
  const hostRequire = createRequire(path.join(host, "package.json"));
  const file = path.join(profile, "codex", "config.toml");
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, "");
  await page.evaluate(
    (file) => window.dock.automation({ kind: "selectConfig", file }),
    file,
  );
  await page.evaluate(() =>
    window.dock.automation({
      kind: "configure",
      enabled: true,
      allowWrite: true,
      port: 0,
    }),
  );
  await page.evaluate(() =>
    window.dock.automation({ kind: "setExecution", allowed: true }),
  );
  const registration = await page.evaluate(() =>
    window.dock.automation({ kind: "register" }),
  );
  assert.equal(registration.state.registration, "registered");
  const headers = hostRequire("smol-toml").parse(fs.readFileSync(file, "utf8"))
    .mcp_servers[registration.state.serverId].http_headers;
  const { Client } = hostRequire("@modelcontextprotocol/sdk/client/index.js");
  const { StreamableHTTPClientTransport } = hostRequire(
    "@modelcontextprotocol/sdk/client/streamableHttp.js",
  );
  const client = new Client({
    name: "Applet public settings regression",
    version: "1",
  });
  try {
    await client.connect(
      new StreamableHTTPClientTransport(new URL(registration.state.endpoint), {
        requestInit: { headers },
      }),
    );
    const call = async (name, args = {}) => {
      const response = await client.callTool({ name, arguments: args });
      assert.notEqual(
        response.isError,
        true,
        JSON.stringify(response.structuredContent),
      );
      return response.structuredContent;
    };
    const get = () => call("appdock_get_settings", { appletId: id });
    const before = await get();
    assert.deepEqual(Object.keys(before.values).sort(), [...keys].sort());
    const schema = await call("appdock_get_settings_schema", { appletId: id });
    assert.deepEqual(Object.keys(schema.fields).sort(), [...keys].sort());
    const catalog = (await call("appdock_list_commands")).commands.filter(
      (c) => c.appletId === id,
    );
    const update = catalog.find((c) => c.id === id + ".settings.update");
    assert(update?.available);
    assert.deepEqual(
      Object.keys(update.inputSchema.properties.changes.properties).sort(),
      [...keys].sort(),
    );
    for (const key of hidden)
      assert(!JSON.stringify(catalog).includes('"' + key + '"'));
    const run = (args) =>
      call("appdock_execute_command", { id: update.id, args });
    assert.equal(
      (await run({ changes, expectedRevision: before.revision, dryRun: true }))
        .completion,
      "validated",
    );
    assert.deepEqual(await get(), before, "dryRun changed state");
    for (const bad of invalid) {
      const result = await client.callTool({
        name: "appdock_execute_command",
        arguments: {
          id: update.id,
          args: { changes: bad, expectedRevision: before.revision },
        },
      });
      assert.equal(result.isError, true);
      assert.deepEqual(await get(), before, "invalid patch changed state");
    }
    await page.evaluate(
      (port) =>
        window.dock.automation({
          kind: "configure",
          enabled: true,
          port,
          allowWrite: false,
        }),
      Number(new URL(registration.state.endpoint).port),
    );
    const denied = await client.callTool({
      name: "appdock_execute_command",
      arguments: {
        id: update.id,
        args: { changes, expectedRevision: before.revision },
      },
    });
    assert.equal(denied.isError, true);
    await page.evaluate(
      (port) =>
        window.dock.automation({
          kind: "configure",
          enabled: true,
          port,
          allowWrite: true,
        }),
      Number(new URL(registration.state.endpoint).port),
    );
    assert.equal(
      (await run({ changes, expectedRevision: before.revision })).completion,
      "settingsSaved",
    );
    const after = await get();
    for (const [key, value] of Object.entries(changes))
      assert.deepEqual(after.values[key], value);
    assert.notEqual(after.revision, before.revision);
    const stale = await client.callTool({
      name: "appdock_execute_command",
      arguments: {
        id: update.id,
        args: { changes: before.values, expectedRevision: before.revision },
      },
    });
    assert.equal(stale.isError, true);
    assert.deepEqual(await get(), after);
    for (const key of switches) {
      for (const [action, expected] of [
        ["off", false],
        ["on", true],
        ["toggle", false],
      ]) {
        const command = id + ".settings." + key + "." + action;
        assert(catalog.some((c) => c.id === command));
        await call("appdock_execute_command", { id: command });
        assert.equal((await get()).values[key], expected);
      }
    }
    const snapshot = await page.evaluate(() => window.dock.snapshot());
    assert(
      snapshot.logs.some(
        (l) => l.source === "automation" && l.message.includes(update.id),
      ),
    );
    return await get();
  } finally {
    await client.close();
    await page.evaluate(() => window.dock.automation({ kind: "unregister" }));
    await page.evaluate(() =>
      window.dock.automation({
        kind: "configure",
        enabled: false,
        port: 0,
        allowWrite: false,
      }),
    );
  }
};
