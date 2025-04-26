/// <summary>
/// Interface for settings management.
/// This interface defines methods for saving and loading settings.
/// </summary>
public interface ISettings 
{
    /// <summary>
    /// Saves the settings.
    /// </summary>
    void Save();

    /// <summary>
    /// Loads the settings.
    /// </summary>
    void Load();
}
