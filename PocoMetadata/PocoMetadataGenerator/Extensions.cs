using System.Collections.Generic;

namespace Breeze.PocoMetadata
{
    public static class Extensions
    {
        /// <summary>
        /// Return the matching value, or null if not found.
        /// </summary>
        /// <param name=""></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static T Get<K,T>(this Dictionary<K, T> map, K key)
        {
            T obj;
            if (map.TryGetValue(key, out obj))
                return obj;
            return default(T);
        }
    }
}
