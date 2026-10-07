document.addEventListener("click", (event) => {
    const button = event.target.closest("[data-password-toggle]");
    if (!button) return;
    const password = button.closest(".input-group-custom").querySelector("input");
    const show = password.type === "password";
    password.type = show ? "text" : "password";
    button.setAttribute("aria-pressed", String(show));
    button.setAttribute("aria-label", show ? "Şifreyi gizle" : "Şifreyi göster");
});

document.addEventListener("submit", (event) => {
    const form = event.target;
    if (!(form instanceof HTMLFormElement) || !form.hasAttribute("data-login-form")) return;
    const button = form.querySelector('button[type="submit"]');
    if (button.disabled) {
        event.preventDefault();
        return;
    }
    button.disabled = true;
    button.setAttribute("aria-busy", "true");
    button.querySelector(".button-label").textContent = "Giriş yapılıyor…";
});

window.addEventListener("pageshow", () => {
    const button = document.querySelector('[data-login-form] button[type="submit"]');
    if (!button) return;
    button.disabled = false;
    button.removeAttribute("aria-busy");
    button.querySelector(".button-label").textContent = "Giriş Yap";
});
