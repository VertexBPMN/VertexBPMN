(() => {
    const intervalMilliseconds = 60_000;
    let refreshInProgress = false;
    let sessionExpired = false;

    function showSessionExpired() {
        sessionExpired = true;
        const notice = document.createElement("aside");
        notice.id = "vertexbpmn-session-expired";
        notice.setAttribute("role", "alert");
        notice.style.cssText = "position:fixed;top:0;left:0;right:0;z-index:10000;padding:1rem;background:#fff3cd;color:#332701;border-bottom:2px solid #856404";
        const message = document.createElement("p");
        message.textContent = "Your session has expired. Server actions require a new sign-in. This page will not reload automatically. Save/export any available editor changes before reloading.";
        const login = document.createElement("a");
        login.href = "/authentication/login";
        login.target = "_blank";
        login.rel = "noopener noreferrer";
        login.textContent = "Sign in in a new tab";
        notice.append(message, login);
        document.body.append(notice);
    }

    async function refreshSession() {
        if (sessionExpired || refreshInProgress || document.visibilityState !== "visible") {
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
                showSessionExpired();
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
            // Retry transient failures. An expired session requires explicit sign-in.
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
