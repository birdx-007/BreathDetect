using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class SerializableDictionary{}

[Serializable]
public struct SerializableKeyValuePair<TKey, TValue>
{
    public TKey key;
    public TValue value;

    public SerializableKeyValuePair(TKey key, TValue value)
    {
        this.key = key;
        this.value = value;
    }
}

[Serializable]
public class SerializableDictionary<TKey, TValue> : SerializableDictionary, ISerializationCallbackReceiver,
    IDictionary<TKey, TValue>
{
    [SerializeField] private List<SerializableKeyValuePair<TKey, TValue>> pairs = new();
    private Lazy<Dictionary<TKey, int>> _keyPositions;
    private Dictionary<TKey, int> KeyPositions => _keyPositions.Value;

    public SerializableDictionary()
    {
        _keyPositions = new Lazy<Dictionary<TKey, int>>(GenerateKeyPositions);
    }

    private Dictionary<TKey, int> GenerateKeyPositions()
    {
        Dictionary<TKey, int> dict = new Dictionary<TKey, int>(pairs.Count);
        for (int i = 0; i < pairs.Count; i++)
        {
            dict[pairs[i].key] = i;
        }
        return dict;
    }
    public void OnBeforeSerialize(){}

    public void OnAfterDeserialize()
    {
        _keyPositions = new Lazy<Dictionary<TKey, int>>(GenerateKeyPositions);
    }

    #region IDictionary<TKey,TValue>

    public TValue this[TKey key]
    {
        get => pairs[KeyPositions[key]].value;
        set
        {
            var pair = new SerializableKeyValuePair<TKey, TValue>(key, value);
            if (KeyPositions.ContainsKey(key))
            {
                pairs[KeyPositions[key]] = pair;
            }
            else
            {
                KeyPositions[key] = pairs.Count;
                pairs.Add(pair);
            }
        }
    }

    public ICollection<TKey> Keys => pairs.Select(tuple => tuple.key).ToArray();
    public ICollection<TValue> Values => pairs.Select(tuple => tuple.value).ToArray();

    public void Add(TKey key, TValue value)
    {
        if (KeyPositions.ContainsKey(key))
        {
            throw new ArgumentException("Repetitive key " + key);
        }

        KeyPositions[key] = pairs.Count;
        pairs.Add(new SerializableKeyValuePair<TKey, TValue>(key,value));
    }

    public bool ContainsKey(TKey key) => KeyPositions.ContainsKey(key);

    public bool Remove(TKey key)
    {
        if (!KeyPositions.TryGetValue(key, out var index))
        {
            return false;
        }

        KeyPositions.Remove(key);
        pairs.RemoveAt(index);
        for (int i = index; i < pairs.Count; i++)
        {
            KeyPositions[pairs[i].key] = i;
        }
        return true;
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        if (KeyPositions.ContainsKey(key))
        {
            value = pairs[KeyPositions[key]].value;
            return true;
        }
        else
        {
            value = default;
            return false;
        }
    }
    #endregion

    #region ICollection<TKey,TValue>

    public int Count => pairs.Count;
    public bool IsReadOnly => false;
    public void Add(KeyValuePair<TKey, TValue> pair) => Add(pair.Key, pair.Value);

    public void Clear()
    {
        pairs.Clear();
        KeyPositions.Clear();
    }

    public bool Contains(KeyValuePair<TKey, TValue> pair) => ContainsKey(pair.Key);

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        int num = pairs.Count;
        if (array.Length < num + arrayIndex)
        {
            throw new ArgumentException("arrayIndex is too large");
        }

        for (int i = 0; i < num; i++)
        {
            var item = pairs[i];
            array[i + arrayIndex] = new KeyValuePair<TKey, TValue>(item.key, item.value);
        }
    }

    public bool Remove(KeyValuePair<TKey, TValue> pair) => Remove(pair.Key);

    #endregion

    #region IEnumerable<KeyValuePair<TKey,TValue>>

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return pairs.Select(ToKeyValuePair).GetEnumerator();

        static KeyValuePair<TKey, TValue> ToKeyValuePair(SerializableKeyValuePair<TKey, TValue> pair)
        {
            return new KeyValuePair<TKey, TValue>(pair.key, pair.value);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion

    public Dictionary<TKey, TValue> CopyToDictionary()
    {
        Dictionary<TKey, TValue> result = new Dictionary<TKey, TValue>();
        foreach (var pair in this)
        {
            result.Add(pair.Key,pair.Value);
        }
        return result;
    }

    public void CopyFromDictionary(Dictionary<TKey, TValue> dictionary)
    {
        Clear();
        foreach (var pair in dictionary)
        {
            Add(pair);
        }
    }
}

#if UNITY_EDITOR
public class SerializableDictionaryDrawer:PropertyDrawer
{
    private SerializedProperty listProperty;

    private SerializedProperty GetListProperty(SerializedProperty property) =>
        listProperty ??= property.FindPropertyRelative("pairs");

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.PropertyField(position, GetListProperty(property), label, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(GetListProperty(property), true);
    }
}
#endif
