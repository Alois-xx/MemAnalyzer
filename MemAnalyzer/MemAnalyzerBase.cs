using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemAnalyzer
{
    /// <summary>
    /// Used by various MemoryAnalyzers which share common functionality.
    /// </summary>
    class MemAnalyzerBase : IDisposable
    {
        protected ClrHeap Heap;
        protected ClrHeap Heap2;
        protected bool LiveOnly;
        protected DisplayUnit DisplayUnit;


        public MemAnalyzerBase(ClrHeap heap, ClrHeap heap2, bool liveOnly, DisplayUnit displayUnit)
        {
            if( heap == null )
            {
                throw new ArgumentNullException("heap was null.");
            }
            Heap = heap;
            Heap2 = heap2;
            LiveOnly = liveOnly;
            DisplayUnit = displayUnit;
        }

        protected static IEnumerable<ulong> GetObjectAddresses(ClrHeap heap, bool bLiveOnly)
        {
            if (bLiveOnly)
            {
                ObjectSet liveObjects = GetLiveObjects(heap);
                List<ulong> live = new List<ulong>();
                foreach (ClrSegment seg in heap.Segments)
                {
                    for (ulong obj = seg.FirstObject; obj != 0; obj = seg.NextObject(obj))
                    {
                        if (liveObjects.Contains(obj))
                        {
                            live.Add(obj);
                        }
                    }
                }
                return live;
            }
            else
            {
                return heap.EnumerateObjectAddresses();
            }
        }


        /// <summary>
        /// See http://stackoverflow.com/questions/35268695/what-is-the-clrmd-equivalent-to-dumpheap-live
        /// </summary>
        /// <param name="heap"></param>
        /// <returns></returns>
        private static ObjectSet GetLiveObjects(ClrHeap heap)
        {
            ObjectSet considered = new ObjectSet(heap);
            Stack<ulong> eval = new Stack<ulong>();

            foreach (var root in heap.EnumerateRoots())
                eval.Push(root.Object);

            while (eval.Count > 0)
            {
                ulong obj = eval.Pop();
                if (considered.Contains(obj))
                    continue;

                considered.Add(obj);

                var type = heap.GetObjectType(obj);
                if (type == null)  // Only if heap corruption
                    continue;

                type.EnumerateRefsOfObjectCarefully(obj, delegate (ulong child, int offset)
                {
                    if (child != 0 && !considered.Contains(child))
                        eval.Push(child);
                });
            }

            return considered;
        }

        /// <summary>
        /// Taken from https://github.com/Microsoft/dotnetsamples/blob/master/Microsoft.Diagnostics.Runtime/CLRMD/docs/WalkingTheHeap.md
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="count"></param>
        /// <param name="size"></param>
        static protected void GetObjSize(ClrHeap heap, ulong obj, ObjectSet farReachingParents, out uint count, out ulong size)
        {
            // Evaluation stack
            Stack<ulong> eval = new Stack<ulong>();

            // To make sure we don't count the same object twice, we'll keep a set of all objects
            // we've seen before.  Note the ObjectSet here is basically just "HashSet<ulong>".
            // However, HashSet<ulong> is *extremely* memory inefficient.  So we use our own to
            // avoid OOMs.
            ObjectSet considered = new ObjectSet(heap);

            count = 0;
            size = 0;
            eval.Push(obj);

            while (eval.Count > 0)
            {
                // Pop an object, ignore it if we've seen it before.
                obj = eval.Pop();
                if (farReachingParents.Contains(obj))
                {
                    continue;
                }

                if (considered.Contains(obj))
                    continue;

                considered.Add(obj);

                // Grab the type. We will only get null here in the case of heap corruption.
                ClrType type = heap.GetObjectType(obj);
                if (type == null)
                    continue;

                count++;
                size += type.GetSize(obj);

                // Now enumerate all objects that this object points to, add them to the
                // evaluation stack if we haven't seen them before.
                type.EnumerateRefsOfObject(obj, delegate (ulong child, int offset)
                {
                    if (child != 0 && !considered.Contains(child) && !farReachingParents.Contains(child))
                    {
                        eval.Push(child);
                    }
                });
            }
        }

        static protected TypeInstance GetObjectReference(ClrHeap heap, ulong objAddress, string fieldName, bool bInner = false)
        {
            ClrType type = heap.GetObjectType(objAddress);
            if (type == null)
            {
                throw new ArgumentException(String.Format("Object at address: {0:X} not found", objAddress));
            }

            return GetObjectReference(heap, objAddress, type, fieldName, bInner);
        }

        static protected TypeInstance GetObjectReference(ClrHeap heap, ulong objAddress, ClrType type, string fieldName, bool bInner = false)
        {
            var fieldType = type.GetFieldByName(fieldName);
            if (fieldType == null)
            {
                throw new ArgumentException(String.Format("Field {0} not found in type {1}", fieldName, type.Name));
            }
            ulong objAdr = fieldType.GetAddress(objAddress, bInner);
            if (!heap.ReadPointer(objAdr, out objAdr))
            {
                throw new InvalidOperationException(String.Format("Could not read from address 0x{0:X} which was field {1} in type {2}. Object Address: 0x:{3:X}", objAddress, fieldName, type.Name, objAddress));
            }
            return new TypeInstance(objAdr, heap.GetObjectType(objAdr));
        }

        static protected T GetObjectValue<T>(ulong objAddress, ClrType type, string fieldName)
        {
            var fieldType = type.GetFieldByName(fieldName);
            if (fieldType == null)
            {
                throw new ArgumentException(String.Format("Could not get field value of object 0x{0:X} for of field {1} of type {2}", objAddress, fieldName, type));
            }
            return (T)fieldType.GetValue(objAddress);
        }

        static protected T GetObjectValue<T>(TypeInstance instance, string fieldName)
        {
            return GetObjectValue<T>(instance.Address, instance.Type, fieldName);
        }


        public void Dispose()
        {
        }
    }
}
