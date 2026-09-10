(() => {
    const intervalMilliseconds = 60_000;
    let refreshInProgress = false;

    async function refreshSession() {
        if (refreshInProgress || document.visibilityState !== "visible") {
            return;
        }

        const token = document.querySelector('meta[name="vertexbpmn-antiforgery"]')?.content;
        if (!token) {
            return;
        }

        refreshInProgress = true;
        try {
            const response = await fetch("/authentication/session/refresh", {
                method: "POST",
                credentials: "same-origin",
                cache: "no-store",
                headers: {
                    "RequestVerificationToken": token,
                    "X-Requested-With": "XMLHttpRequest"
                }
            });

            if (response.status === 401) {
                window.location.assign("/authentication/login");
                return;
            }
            if (!response.ok) {
                return;
            }

            const result = await response.json();
            if (result.renewed === true) {
                window.location.reload();
            }
        } finally {
            refreshInProgress = false;
        }
    }

    window.setInterval(refreshSession, intervalMilliseconds);
    document.addEventListener("visibilitychange", () => {
        if (document.visibilityState === "visible") {
            void refreshSession();
        }
    });
})();
