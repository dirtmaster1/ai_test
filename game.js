const legacyRedirectTarget = 'index.html';

if (window.location.pathname.endsWith('/grid_scene.html')) {
    window.location.replace(legacyRedirectTarget);
} else {
    const message = document.createElement('div');
    message.style.minHeight = '100vh';
    message.style.display = 'flex';
    message.style.alignItems = 'center';
    message.style.justifyContent = 'center';
    message.style.padding = '24px';
    message.style.background = 'linear-gradient(180deg, #141311 0%, #090909 100%)';
    message.style.color = '#ebe1cb';
    message.style.fontFamily = "Georgia, 'Times New Roman', serif";
    message.style.textAlign = 'center';
    message.innerHTML = '<div><h1 style="font-size: 32px; margin-bottom: 12px;">Dark Dungeon Tactics</h1><p style="font-size: 16px; color: #b9ab8c;">This legacy entry script is deprecated. Opening the tactical root experience instead.</p></div>';
    document.body.appendChild(message);
    window.setTimeout(() => {
        window.location.replace(legacyRedirectTarget);
    }, 800);
}
