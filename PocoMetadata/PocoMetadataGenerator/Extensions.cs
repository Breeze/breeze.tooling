using System.Collections.Generic;

namespace Breeze.PocoMetadata
{
    /// <summary>
    /// Extension methods that are useful in this project
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Return the matching value, or null if not found.
        /// </summary>
        /// <param name="map">This dictionary</param>
        /// <param name="key">Key for the dictionary</param>
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
