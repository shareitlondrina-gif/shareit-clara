(() => {
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
        }
    });

    const modal = document.getElementById('modalIndicador');
    const conteudo = document.getElementById('modalIndicadorConteudo');
    if (!modal || !conteudo) return;

    const fechar = () => {
        modal.hidden = true;
        modal.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('modal-aberto');
    };
    const abrir = async (url) => {
        modal.hidden = false;
        modal.setAttribute('aria-hidden', 'false');
        document.body.classList.add('modal-aberto');
        conteudo.innerHTML = '<div class="carregando-modal">Carregando detalhes…</div>';
        try {
            const resposta = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!resposta.ok) throw new Error(`HTTP ${resposta.status}`);
            conteudo.innerHTML = await resposta.text();
        } catch {
            conteudo.innerHTML = '<div class="erro-modal"><strong>Não foi possível carregar o detalhamento.</strong><span>Tente novamente em alguns instantes.</span></div>';
        }
    };

    document.addEventListener('click', e => {
        const alvo = e.target.closest('[data-drilldown-url]');
        if (alvo) {
            e.preventDefault();
            abrir(alvo.getAttribute('data-drilldown-url'));
            return;
        }
        if (e.target.closest('[data-modal-fechar]')) fechar();
    });
    document.addEventListener('keydown', e => { if (e.key === 'Escape') fechar(); });
})();
