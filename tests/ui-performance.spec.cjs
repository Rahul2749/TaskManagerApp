const assert = require("node:assert/strict");
const fs = require("node:fs");
const vm = require("node:vm");
const source = fs.readFileSync("src/TaskManager/TaskManager/TaskManager/wwwroot/js/razorpay-checkout.js", "utf8");
function environment() {
    const scripts = [];
    const timers = new Map();
    let nextTimer = 0;
    const context = { window: {}, setTimeout(callback, ms) {
        assert.equal(ms, 20000);
        timers.set(++nextTimer, callback);
        return nextTimer;
    }, clearTimeout(id) { timers.delete(id); }, document: {
        createElement: () => ({ remove() {} }),
        head: { appendChild: script => scripts.push(script) }
    }};
    vm.createContext(context);
    vm.runInContext(source, context);
    return { context, scripts, timers };
}
function checkoutProvider(context) {
    context.Razorpay = function(options) {
        this.open = () => options.handler({ razorpay_payment_id: "pay_test", razorpay_subscription_id: "sub_test", razorpay_signature: "signed" });
    };
}
(async () => {
    const { context, scripts } = environment();
    assert.equal(scripts.length, 0, "gateway loaded before checkout");
    const first = context.window.taskManagerRazorpay.openSubscription({});
    const second = context.window.taskManagerRazorpay.openSubscription({});
    assert.equal(scripts.length, 1, "concurrent checkout duplicated gateway download");
    checkoutProvider(context);
    scripts[0].onload();
    assert.equal((await first).subscriptionId, "sub_test");
    assert.equal((await second).paymentId, "pay_test");
    await context.window.taskManagerRazorpay.openSubscription({});
    assert.equal(scripts.length, 1, "gateway was downloaded again");
    const failed = environment();
    const attempt = failed.context.window.taskManagerRazorpay.openSubscription({});
    failed.scripts[0].onerror();
    await assert.rejects(attempt, /Unable to load checkout/);
    const retry = failed.context.window.taskManagerRazorpay.openSubscription({});
    assert.equal(failed.scripts.length, 2, "gateway failure prevented retry");
    checkoutProvider(failed.context);
    failed.scripts[1].onload();
    await retry;
    const timedOut = environment();
    const slow = timedOut.context.window.taskManagerRazorpay.openSubscription({});
    [...timedOut.timers.values()][0]();
    await assert.rejects(slow, /Unable to load checkout/);
    const afterTimeout = timedOut.context.window.taskManagerRazorpay.openSubscription({});
    timedOut.scripts[0].onload(); // A stale callback must not reset the new attempt.
    checkoutProvider(timedOut.context);
    timedOut.scripts[1].onload();
    await afterTimeout;
    const shell = fs.readFileSync("src/TaskManager/TaskManager/TaskManager/Components/App.razor", "utf8");
    assert.ok(shell.includes("<ResourcePreloader />"));
    assert.ok(!shell.includes("setTimeout") && !shell.includes("sessionStorage") && !shell.includes("gsap"));
    assert.ok(!shell.includes('src="https://checkout.razorpay.com'));
    assert.ok(!shell.includes("unpkg.com"));
    const layout = fs.readFileSync("src/TaskManager/TaskManager/TaskManager.Client/Components/Layout/MainLayout.razor", "utf8");
    assert.ok(!layout.includes("MutationObserver") && !layout.includes("gsap") && !layout.includes("setInterval"));
    assert.ok(!layout.includes("Notifications.Changed") && !layout.includes("StateHasChanged"), "notification updates can rerender page content");
    const header = fs.readFileSync("src/TaskManager/TaskManager/TaskManager.Client/Components/Layout/Header.razor", "utf8");
    assert.ok(header.includes("<NotificationBell />"));
    for (const page of ["Manager/ManageTasks", "Manager/ManageUsers", "Admin/ManageManagers", "Admin/AuditLog", "Billing/ManageBilling"]) {
        const markup = fs.readFileSync(`src/TaskManager/TaskManager/TaskManager.Client/Pages/${page}.razor`, "utf8");
        assert.ok(markup.includes('RowsPerPage="20"') && markup.includes("<MudTablePager"), `${page} lost bounded rows or its pager`);
    }
    console.log("PASS checkout lazy loading, concurrent reuse, failure/retry and startup/layout source checks");
})().catch(error => { console.error(error); process.exitCode = 1; });
