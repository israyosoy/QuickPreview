using System.Windows.Media.Imaging;

namespace QuickPreview.Services;

// Thread-safe LRU cache for decoded BitmapSource objects.
// Capacity 6: covers current + 2 ahead + 2 behind with one spare slot.
public sealed class BitmapCache
{
    public static readonly BitmapCache Instance = new(capacity: 6);

    private readonly int _capacity;
    private readonly Dictionary<string, LinkedListNode<(string key, BitmapSource bitmap)>> _map;
    private readonly LinkedList<(string key, BitmapSource bitmap)> _list;
    private readonly object _lock = new();

    private BitmapCache(int capacity)
    {
        _capacity = capacity;
        _map = new Dictionary<string, LinkedListNode<(string, BitmapSource)>>(StringComparer.OrdinalIgnoreCase);
        _list = new LinkedList<(string, BitmapSource)>();
    }

    public BitmapSource? Get(string path)
    {
        lock (_lock)
        {
            if (!_map.TryGetValue(path, out var node)) return null;
            _list.Remove(node);
            _list.AddFirst(node);
            return node.Value.bitmap;
        }
    }

    public void Put(string path, BitmapSource bitmap)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(path, out var existing))
            {
                _list.Remove(existing);
                _map.Remove(path);
            }

            var node = _list.AddFirst((path, bitmap));
            _map[path] = node;

            while (_list.Count > _capacity)
            {
                var lru = _list.Last!;
                _map.Remove(lru.Value.key);
                _list.RemoveLast();
            }
        }
    }

    public bool Contains(string path)
    {
        lock (_lock) return _map.ContainsKey(path);
    }
}
