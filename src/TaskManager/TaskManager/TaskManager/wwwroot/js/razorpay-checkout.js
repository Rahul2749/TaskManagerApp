let razorpayScript;
function ensureRazorpay() {
  if (typeof Razorpay !== "undefined") return Promise.resolve();
  if (!razorpayScript) {
    razorpayScript = new Promise((resolve, reject) => {
      const script = document.createElement("script");
      let finished = false;
      script.src = "https://checkout.razorpay.com/v1/checkout.js";
      const timeout = setTimeout(() => script.onerror(), 20000);
      script.onload = () => {
        if (finished) return;
        if (typeof Razorpay === "undefined") { script.onerror(); return; }
        finished = true;
        clearTimeout(timeout);
        resolve();
      };
      script.onerror = () => {
        if (finished) return;
        finished = true;
        clearTimeout(timeout);
        script.remove();
        razorpayScript = null;
        reject(new Error("Unable to load checkout. Please retry."));
      };
      document.head.appendChild(script);
    });
  }
  return razorpayScript;
}
window.taskManagerRazorpay = {
  openSubscription: async function (options) {
    await ensureRazorpay();
    return new Promise(function (resolve, reject) {
      if (typeof Razorpay === "undefined") {
        reject(new Error("Razorpay Checkout script failed to load."));
        return;
      }

      var rzp = new Razorpay({
        key: options.key,
        subscription_id: options.subscriptionId,
        name: options.name || "TaskManager",
        description: options.description || "Subscription",
        prefill: {
          name: options.customerName || "",
          email: options.customerEmail || ""
        },
        theme: { color: "#4F46E5" },
        handler: function (response) {
          resolve({
            paymentId: response.razorpay_payment_id || null,
            subscriptionId: response.razorpay_subscription_id || null,
            signature: response.razorpay_signature || null
          });
        },
        modal: {
          ondismiss: function () {
            resolve(null);
          }
        }
      });

      rzp.open();
    });
  }
};
