namespace _Project.Scripts.Network
{
    public enum NetworkGameMode
    {
        Solo,            // Одиночная оффлайн игра без инициализации сети
        LanHost,         // Создание локального сервера (без интернета)
        LanClient,       // Подключение к локальному серверу по прямому IPv4
        OnlineHost,      // Создание комнаты в облаке Photon Cloud
        OnlineClient     // Подключение к комнате по имени в облаке Photon Cloud
    }
}