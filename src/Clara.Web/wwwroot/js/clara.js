// Clara
// Programador: Paulo Andrade
// Objetivo: comportamento cliente/extensão da Clara.
// Origem: navegador e interface da Clara.
// Destino: interface ou canal autorizado do agente.
// Boas práticas: mínimo privilégio, sem conteúdo sensível e validação de mensagens.

(() => {
    const menuUsuario = document.querySelector('[data-menu-usuario]');
    const botaoMenuUsuario = document.querySelector('[data-menu-usuario-botao]');

    const fecharMenuUsuario = () => {
        if (!menuUsuario || !botaoMenuUsuario) return;
        menuUsuario.hidden = true;
        botaoMenuUsuario.setAttribute('aria-expanded', 'false');
    };

    if (botaoMenuUsuario && menuUsuario) {
        botaoMenuUsuario.addEventListener('click', e => {
            e.preventDefault();
            e.stopPropagation();
            const abrir = menuUsuario.hidden;
            menuUsuario.hidden = !abrir;
            botaoMenuUsuario.setAttribute('aria-expanded', abrir ? 'true' : 'false');
        });
    }

    const painelNotificacoes = document.getElementById('painelNotificacoes');
    const botaoNotificacoes = document.querySelector('[data-notificacoes-url]');
    const badgeNotificacoes = document.querySelector('[data-notificacoes-badge]');

    const atualizarBadgeNotificacoes = async () => {
        if (!botaoNotificacoes || !badgeNotificacoes) return;
        try {
            const resposta = await fetch('/notificacoes/contagem', {
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                credentials: 'same-origin'
            });
            if (!resposta.ok) return;
            const dados = await resposta.json();
            const total = Number(dados.naoLidas || 0);
            badgeNotificacoes.textContent = total > 99 ? '99+' : String(total);
            badgeNotificacoes.hidden = total <= 0;
            botaoNotificacoes.classList.toggle('tem-notificacao', total > 0);
            botaoNotificacoes.setAttribute('aria-label', total > 0 ? `Notificações: ${total} não lida(s)` : 'Notificações');
        } catch {
            // A barra superior continua funcional mesmo se a contagem estiver temporariamente indisponível.
        }
    };

    const fecharNotificacoes = () => {
        if (!painelNotificacoes || !botaoNotificacoes) return;
        painelNotificacoes.hidden = true;
        painelNotificacoes.setAttribute('aria-hidden', 'true');
        botaoNotificacoes.setAttribute('aria-expanded', 'false');
    };

    const abrirNotificacoes = async () => {
        if (!painelNotificacoes || !botaoNotificacoes) return;
        const aberto = !painelNotificacoes.hidden;
        if (aberto) { fecharNotificacoes(); return; }
        painelNotificacoes.hidden = false;
        painelNotificacoes.setAttribute('aria-hidden', 'false');
        botaoNotificacoes.setAttribute('aria-expanded', 'true');
        painelNotificacoes.innerHTML = '<div class="carregando-modal">Carregando notificações…</div>';
        try {
            const resposta = await fetch(botaoNotificacoes.getAttribute('data-notificacoes-url') || '/notificacoes/painel', {
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                credentials: 'same-origin'
            });
            if (!resposta.ok) throw new Error(`HTTP ${resposta.status}`);
            painelNotificacoes.innerHTML = await resposta.text();
            await atualizarBadgeNotificacoes();
        } catch {
            painelNotificacoes.innerHTML = '<div class="erro-modal"><strong>Não foi possível carregar as notificações.</strong><span>Tente novamente em alguns instantes.</span></div>';
        }
    };

    if (botaoNotificacoes) {
        botaoNotificacoes.addEventListener('click', e => {
            e.preventDefault();
            e.stopPropagation();
            abrirNotificacoes();
        });
        atualizarBadgeNotificacoes();
        window.setInterval(atualizarBadgeNotificacoes, 60000);
    }

    document.addEventListener('click', async e => {
        const botaoCopiar = e.target.closest('[data-copiar-alvo]');
        if (botaoCopiar) {
            const campo = document.getElementById(botaoCopiar.getAttribute('data-copiar-alvo'));
            if (!campo) return;
            try {
                await navigator.clipboard.writeText(campo.value || campo.textContent || '');
                const original = botaoCopiar.textContent;
                botaoCopiar.textContent = 'Copiado';
                setTimeout(() => botaoCopiar.textContent = original, 1600);
            } catch {
                campo.select?.();
                document.execCommand?.('copy');
            }
            return;
        }

        if (painelNotificacoes && !painelNotificacoes.hidden &&
            !painelNotificacoes.contains(e.target) && !botaoNotificacoes?.contains(e.target)) {
            fecharNotificacoes();
        }
        if (menuUsuario && !menuUsuario.hidden && !menuUsuario.contains(e.target) && !botaoMenuUsuario?.contains(e.target)) {
            fecharMenuUsuario();
        }
    });

    const modal = document.getElementById('modalIndicador');
    const conteudo = document.getElementById('modalIndicadorConteudo');

    const fecharModal = () => {
        if (!modal) return;
        modal.hidden = true;
        modal.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('modal-aberto');
    };

    const abrirModal = async (url) => {
        if (!modal || !conteudo) return;
        modal.hidden = false;
        modal.setAttribute('aria-hidden', 'false');
        document.body.classList.add('modal-aberto');
        conteudo.innerHTML = '<div class="carregando-modal">Carregando detalhes…</div>';
        try {
            const resposta = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' }, credentials: 'same-origin' });
            if (!resposta.ok) throw new Error(`HTTP ${resposta.status}`);
            conteudo.innerHTML = await resposta.text();
        } catch {
            conteudo.innerHTML = '<div class="erro-modal"><strong>Não foi possível carregar o detalhamento.</strong><span>Tente novamente em alguns instantes.</span></div>';
        }
    };

    document.addEventListener('click', e => {
        const alvo = e.target.closest('[data-drilldown-url]');
        if (alvo && modal && conteudo) {
            e.preventDefault();
            abrirModal(alvo.getAttribute('data-drilldown-url'));
            return;
        }
        if (e.target.closest('[data-modal-fechar]')) fecharModal();
    });

    document.addEventListener('keydown', e => {
        if (e.key !== 'Escape') return;
        fecharModal();
        fecharNotificacoes();
        fecharMenuUsuario();
    });
})();
