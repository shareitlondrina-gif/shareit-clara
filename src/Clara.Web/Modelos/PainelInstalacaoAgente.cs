// Clara
// Programador: Paulo Andrade
// Objetivo: implementar um componente da Clara.
// Origem: camadas internas da solução.
// Destino: consumidores internos da solução.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

using Clara.Dados.Repositorios;
using Clara.Dominio.Contratos;

namespace Clara.Web.Modelos;

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
