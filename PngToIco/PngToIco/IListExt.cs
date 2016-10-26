using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PngToIco
{
	static class IListExt
	{
		public static void Append<T>(this IList<T> list, IEnumerable<T> data)
		{
			foreach (var element in data)
				list.Add(element);
		}
	}
}
