namespace Scarab.Interfaces;

public interface IInstaller
{
    public enum ReinstallPolicy
    {
        ForceReinstall,
        SkipUpToDate
    }

    public Task Toggle(ModItem mod);

    public Task Cache(ModItem mod, Action<ModProgressArgs> setProgress);

    public Task Install(ModItem mod, Action<ModProgressArgs> setProgress, bool enable);

    public Task Uninstall(ModItem mod);

    public Task InstallApi(ReinstallPolicy policy = ReinstallPolicy.SkipUpToDate);

    public Task ToggleApi();
}