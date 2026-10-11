// Verify the SDK's published asset contract without starting the app or accessing a provider.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const output = process.argv[2];
assert.ok(output, "Pass the Release publish directory.");
const manifest = JSON.parse(fs.readFileSync(path.join(output, "TaskManager.staticwebassets.endpoints.json"), "utf8"));
const endpoints = manifest.Endpoints;
assert.ok(Array.isArray(endpoints), "Missing static asset endpoints.");
for (const pattern of [ /^_framework\/blazor\.web\.[a-z0-9]+\.js$/, /^app\.[a-z0-9]+\.css$/, /^js\/razorpay-checkout\.[a-z0-9]+\.js$/ ]) {
    const matches = endpoints.filter(item => pattern.test(item.Route));
    assert.ok(matches.length, `Missing fingerprinted asset: ${pattern}`);
    assert.ok(matches.some(item => item.ResponseHeaders.some(header => header.Name === "Cache-Control" && header.Value.includes("immutable"))), "Fingerprinted asset lost immutable caching.");
    assert.ok(matches.some(item => item.Selectors.some(selector => selector.Name === "Content-Encoding" && selector.Value === "br")), "Asset lost Brotli negotiation.");
}
const framework = path.join(output, "wwwroot", "_framework");
const bootScripts = fs.readdirSync(framework).filter(name => /^dotnet(?:\.[a-z0-9]+)?\.js$/.test(name));
const config = bootScripts.map(name => fs.readFileSync(path.join(framework, name), "utf8").match(/\/\*json-start\*\/([\s\S]*?)\/\*json-end\*\//))
    .find(Boolean);
assert.ok(config, ".NET 10 inline boot configuration is missing.");
const resources = JSON.parse(config[1]).resources;
const wasm = ["wasmNative", "coreAssembly", "assembly"].flatMap(category => resources[category] ?? []).filter(item => item.name.endsWith(".wasm"));
assert.ok(wasm.length > 0, "No boot WASM resources found.");
let brotliBytes = 0;
for (const resource of wasm) {
    const filename = path.join(framework, resource.name);
    assert.ok(fs.existsSync(filename), `Missing boot resource: ${resource.name}`);
    assert.ok(fs.existsSync(filename + ".br"), `Missing Brotli resource: ${resource.name}`);
    assert.ok(endpoints.some(item => item.Route === `_framework/${resource.name}` && item.Selectors.some(selector => selector.Name === "Content-Encoding" && selector.Value === "br")), `Missing compressed endpoint: ${resource.name}`);
    brotliBytes += fs.statSync(filename + ".br").size;
}
assert.ok(!wasm.some(resource => /IdentityModel|Blazored\.LocalStorage/.test(resource.name)), "Unused browser dependencies returned.");
console.log(`PASS .NET 10 fingerprinting, immutable caching, inline boot config and Brotli endpoints; ${wasm.length} WASM resources, ${brotliBytes} Brotli bytes`);
