namespace SMHackCore {
    using System;

    public sealed class ApiHook {
        public ApiHook(string module, string symbol, Func<ServerInterfaceHookProxy, Delegate> delegateFetcher) {
            Module = module;
            Symbol = symbol;
            DelegateFetcher = delegateFetcher;
        }

        public string Module { get; }
        public string Symbol { get; }
        public Func<ServerInterfaceHookProxy, Delegate> DelegateFetcher { get; }
    }
}