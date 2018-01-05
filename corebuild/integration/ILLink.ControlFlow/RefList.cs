using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ILLink.ControlFlow.Collections
{
    using static System.Diagnostics.Debug;

    internal sealed class RefList<T>
    {
        private T[] data;
        public ref T this[int index] => ref data[index];

        private int count;
        public int Count => count;

        private int Capacity => data.Length;

        public RefList() => Initialize(0, 64);
        public RefList(int count) => Initialize(count, count);

        private void Initialize(int count, int capacity)
        {
            this.data = new T[capacity];
            this.count = count;
#if DEBUG
            this.oldData = new ConditionalWeakTable<T[], OldData>();
            this.oldDataKeys = new List<WeakReference<T[]>>();
#endif
        }

        public void Add(T item)
        {
            if (count == Capacity)
            {
                T[] newData = new T[2 * count];
                data.CopyTo(newData, 0);
#if DEBUG
                oldDataKeys.Add(new WeakReference<T[]>(data));
                oldData.Add(data, new OldData(data));
#endif
                data = newData;
            }
            data[count++] = item;
        }

#if DEBUG
        private ConditionalWeakTable<T[], OldData> oldData;
        private List<WeakReference<T[]>> oldDataKeys;
#endif

        [System.Diagnostics.Conditional("DEBUG")]
        internal void CheckOldData()
        {
#if DEBUG
            for (int i = 0; i < oldDataKeys.Count; ++i)
            {
                if (oldDataKeys[i].TryGetTarget(out T[] oldArray))
                {
                    if (oldData.TryGetValue(oldArray, out OldData oldDatum))
                    {
                        oldDatum.Check();
                    }
                }
            }
#endif
        }

#if DEBUG
        private class OldData
        {
            private T[] array;
            private T[] arrayCopy;

            public OldData(T[] array) {
                this.array = array;
                this.arrayCopy = new T[array.Length];
                array.CopyTo(arrayCopy, 0);
            }

            public void Check() => Assert(array.Equals(arrayCopy));

            ~OldData() => Check();
        }
#endif
    }
}
