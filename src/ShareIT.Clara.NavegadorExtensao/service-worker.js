const HOST = "br.com.shareit.clara";
let ultimoDominio = null;
let ultimaEmissao = 0;
let monitoramentoPermitidoCache = false;
let ultimaConsultaPolitica = 0;

function navegadorAtual() {
  return navigator.userAgent.includes("Edg/") ? "Edge" : "Chrome";
}

async function monitoramentoPermitido() {
  const agora = Date.now();
  if (agora - ultimaConsultaPolitica < 30000) return monitoramentoPermitidoCache;
  ultimaConsultaPolitica = agora;
  try {
    const resposta = await chrome.runtime.sendNativeMessage(HOST, {
      acao: "status",
      navegador: navegadorAtual(),
      momentoUtc: new Date().toISOString()
    });
    monitoramentoPermitidoCache = resposta?.ok === true && resposta?.monitorar === true;
  } catch (_) {
    // Falha fechada: sem host/política válida, não consulta nem envia a aba ativa.
    monitoramentoPermitidoCache = false;
  }
  return monitoramentoPermitidoCache;
}

async function enviarAbaAtiva(forcar = false) {
  try {
    // A checagem acontece antes de consultar a URL da aba. Fora do período configurado,
    // a extensão não lê a aba ativa para fins de telemetria da .Clara.
    if (!(await monitoramentoPermitido())) {
      ultimoDominio = null;
      return;
    }

    const [aba] = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
    if (!aba?.url) return;
    const url = new URL(aba.url);
    if (url.protocol !== "http:" && url.protocol !== "https:") return;
    let dominio = url.hostname.toLowerCase().replace(/^www\./, "");
    if (!dominio || dominio.length > 500) return;

    const agora = Date.now();
    if (!forcar && dominio === ultimoDominio && agora - ultimaEmissao < 45000) return;

    const resposta = await chrome.runtime.sendNativeMessage(HOST, {
      acao: "dominio",
      dominio,
      navegador: navegadorAtual(),
      momentoUtc: new Date().toISOString()
    });
    if (resposta?.monitorar === false) {
      monitoramentoPermitidoCache = false;
      ultimaConsultaPolitica = Date.now();
      ultimoDominio = null;
      return;
    }
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
  if (info.status === "complete") enviarAbaAtiva(true);
});
chrome.windows.onFocusChanged.addListener(() => enviarAbaAtiva(true));
chrome.alarms.onAlarm.addListener(a => {
  if (a.name === "clara-contexto-web") enviarAbaAtiva(true);
});
