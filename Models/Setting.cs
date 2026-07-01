namespace MatriX.GST.Models;

public class Setting
{
    /// <summary>
    /// Порт сервера
    /// </summary>
    public int port { get; set; } = 8590;

    /// <summary>
    /// Режим прослушивания: true - ip:port / false - 127.0.0.1:port
    /// </summary>
    public bool IPAddressAny { get; set; } = true;

    /// <summary>
    /// Время работы ts с момента последнего обращения пользователя (в минутах)
    /// </summary>
    public int worknodetominutes { get; set; } = 5;

    public string tsargs { get; set; }

    public int tsCheckPortTimeout { get; set; } = 15;

    /// <summary>
    /// Использовать lsof для мониторинга системы
    /// </summary>
    public bool lsof { get; set; } = true;
}
