namespace SMHackCore {
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    public static class ObjectExtensions {
        public static T ToObject<T>(this IDictionary<string, object> source)
            where T : class, new() {
            var someObject = new T();
            var someObjectType = someObject.GetType();

            foreach (var item in source)
                someObjectType.GetProperty(item.Key)?.SetValue(someObject, item.Value, null);

            return someObject;
        }

        public static IDictionary<string, object> AsDictionary(
            this object source,
            BindingFlags bindingAttr = BindingFlags.Public | BindingFlags.Instance) {
            return source.GetType().
                GetFields(bindingAttr).
                ToDictionary(
                    propInfo => propInfo.Name,
                    propInfo => propInfo.GetValue(source)
                );
        }
    }
}