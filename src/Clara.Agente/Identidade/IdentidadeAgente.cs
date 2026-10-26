// Clara
// Programador: Paulo Andrade
// Objetivo: executar coleta e comunicação do agente Windows.
// Origem: Windows, configuração local e API Clara.
// Destino: API Clara, telemetria autorizada e estado local.
// Boas práticas: responsabilidade única, validação de entrada, tratamento explícito de falhas e ausência de segredos no código.

namespace Clara.Agente.Identidade;

public sealed record IdentidadeAgente(Guid IdentificadorAgente, string TokenDispositivo, Guid AtivoId, Guid TenantId);
