using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SockLibUnitTests {
    static class Extensions {
        public static void Shuffle<T>(this IList<T> list) {
            var rnd = new Random();
            for (int n = list.Count - 1; n > 1; n--) {
                int k = rnd.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static int Megs(this int m) {
            return m * 1024 * 1024;
        }
    }
}
