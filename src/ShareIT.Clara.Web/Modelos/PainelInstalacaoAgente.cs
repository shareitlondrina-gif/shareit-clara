using ShareIT.Clara.Dados.Repositorios;
using ShareIT.Clara.Dominio.Contratos;

namespace ShareIT.Clara.Web.Modelos;

/// <summary>
/// Objetivo: reunir dados do onboarding do agente em uma tela única e segura para o administrador do tenant.
/// Origem: assinatura SaaS, empresas do tenant e configuração pública do servidor.
/// Destino: Administração > Instalação do agente.
/// Desenvolvedor: Paulo Andrade
/// </summary>
public sealed record PainelInstalacaoAgente(
    IReadOnlyCollection<EmpresaBasica> Empresas,
    AssinaturaTenantResumo Assinatura,
    int EstacoesAtivas,
    string UrlServidor,
    string VersaoAgente);
