window.taskManagerRazorpay = {
  openSubscription: function (options) {
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
