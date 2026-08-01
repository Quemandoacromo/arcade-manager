namespace ArcadeManager.Models;

public class AppSettingsModel
{
    public AppModel App { get; set; }

    public UpdaterModel Updater { get; set; }

    public class AppModel
    {
        public string HomePage { get; set; }
    }

    public class UpdaterModel
    {
        public string Feedurl { get; set; }
    }
}