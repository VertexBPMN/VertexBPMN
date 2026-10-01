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

            // Consume the response without triggering a navigation or resetting drafts.
            await response.json();

            // This response renews the HttpOnly cookie. The circuit revalidates
            // its principal separately, without destroying editor drafts.
        } catch {
            // Retry transient network failures next time. Only 401 starts login.
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
