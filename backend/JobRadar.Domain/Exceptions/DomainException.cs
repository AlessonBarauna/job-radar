namespace JobRadar.Domain.Exceptions;

/// <summary>
/// Exceção de domínio — representa violação de regra de negócio.
/// Mapeada para HTTP 400 pelo GlobalExceptionMiddleware.
/// </summary>
public class DomainException(string message) : Exception(message);
