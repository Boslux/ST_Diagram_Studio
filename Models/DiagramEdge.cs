namespace DiagramApp.Models;

/// <summary>
/// Iki dugum arasindaki yonlu baglanti.
/// </summary>
internal sealed class DiagramEdge
{
    public DiagramEdge(string fromId, string toId)
    {
        FromId = fromId;
        ToId = toId;
    }

    /// <summary>
    /// Kaynak dugum kimligi.
    /// </summary>
    public string FromId { get; }

    /// <summary>
    /// Hedef dugum kimligi.
    /// </summary>
    public string ToId { get; }
}
