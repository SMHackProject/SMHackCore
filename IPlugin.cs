namespace SMHackCore {
    public interface IPlugin {
        ApiHook[] GetApiHooks();
        void Init();
    }
}