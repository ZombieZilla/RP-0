﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RP0.DataTypes
{
    public class PersistentList<T> : List<T>, IConfigNode where T : IConfigNode
    {
        public void Load(ConfigNode node)
        {
            Clear();
            foreach (ConfigNode n in node.nodes)
            {
                T item = System.Activator.CreateInstance<T>();
                item.Load(n);
                Add(item);
            }
        }

        public void Save(ConfigNode node)
        {
            foreach (var item in this)
            {
                ConfigNode n = new ConfigNode("ITEM");
                item.Save(n);
                node.AddNode(n);
            }
        }
    }

    /// <summary>
    /// NOTE: This does not have constraints because string is supported
    /// but string is not a valuetype
    /// </summary>
    public class PersistentListValueType<T> : List<T>, IConfigNode
    {
        private static System.Reflection.MethodInfo ReadValueMethod = typeof(ConfigNode).GetMethod("ReadValue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        private static System.Type type = typeof(T);

        public void Load(ConfigNode node)
        {
            Clear();
            foreach (ConfigNode.Value v in node.values)
            {
                T item = (T)ReadValueMethod.Invoke(null, new object[] { type, v.value });
                Add(item);
            }
        }

        public void Save(ConfigNode node)
        {
            foreach (var item in this)
            {
                node.AddValue("item", item.ToString());
            }
        }
    }
}
