namespace ProductHub.Domain.Roadmap;

/// <summary>
/// Direção de melhoria do KPI, definida no cadastro. É a fonte de verdade para
/// calcular o resultado (Positivo/Negativo) da apuração:
/// HigherIsBetter ("Quanto maior melhor") equivale a "Aumentar"; e
/// LowerIsBetter ("Quanto menor melhor") equivale a "Reduzir".
/// </summary>
public enum KpiOperation
{
    HigherIsBetter = 0,
    LowerIsBetter = 1
}
