const HOST = "br.com.shareit.clara";
let ultimoDominio = null;
let ultimaEmissao = 0;

function navegadorAtual() {
  return navigator.userAgent.includes("Edg/") ? "Edge" : "Chrome";
}

async function enviarAbaAtiva(forcar = false) {
  try {
    const [aba] = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
    if (!aba?.url) return;
    const url = new URL(aba.url);
    if (url.protocol !== "http:" && url.protocol !== "https:") return;
    let dominio = url.hostname.toLowerCase().replace(/^www\./, "");
    if (!dominio || dominio.length > 500) return;

    const agora = Date.now();
    if (!forcar && dominio === ultimoDominio && agora - ultimaEmissao < 45000) return;

    await chrome.runtime.sendNativeMessage(HOST, {
      dominio,
      navegador: navegadorAtual(),
      momentoUtc: new Date().toISOString()
    });
    ultimoDominio = dominio;
    ultimaEmissao = agora;
  } catch (_) {
    // Sem host nativo/agente ou sem aba HTTP(S): não coleta e não interrompe a navegação do usuário.
  }
}

chrome.runtime.onInstalled.addListener(() => {
  chrome.alarms.create("clara-contexto-web", { periodInMinutes: 1 });
  enviarAbaAtiva(true);
});
chrome.runtime.onStartup.addListener(() => enviarAbaAtiva(true));
chrome.tabs.onActivated.addListener(() => enviarAbaAtiva(true));
chrome.tabs.onUpdated.addListener((_id, info) => {
  if (info.status === "complete" || info.url) enviarAbaAtiva(true);
});
chrome.windows.onFocusChanged.addListener(() => enviarAbaAtiva(true));
chrome.alarms.onAlarm.addListener(a => {
  if (a.name === "clara-contexto-web") enviarAbaAtiva(true);
});
